using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Adapters
{
    public class MySqlInspector : SqlInspector
    {
        private readonly string? _defaultSchema;

        public override string Dialect => "MySQL";

        public MySqlInspector(Func<DbConnection> connectionFactory, string? defaultSchema = null) :
            base(connectionFactory)
        {
            _defaultSchema = defaultSchema;
        }

        private async ValueTask<string> ResolveSchemaAsync(
            string? schema, CancellationToken cancellationToken)
        {
            if (schema != null)
                return schema;

            if (_defaultSchema != null)
                return _defaultSchema;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DATABASE()";
            object? result = await cmd.ExecuteScalarAsync(cancellationToken);

            return result as string
                ?? throw new InvalidOperationException("No default database selected.");
        }

        public override async ValueTask<IReadOnlyList<SqlSchemaInfo>> ListSchemasAsync(
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT schema_name, 'N/A' AS schema_owner
                FROM information_schema.schemata
                WHERE schema_name NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')
                ORDER BY schema_name
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<SqlSchemaInfo> results = new();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SqlSchemaInfo(
                    Name: reader.GetString(0),
                    Owner: reader.GetString(1)
                ));
            }

            return results;
        }

        public override async ValueTask<IReadOnlyList<SqlTableInfo>> ListTablesAsync(
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                  AND (@schema IS NULL OR table_schema = @schema)
                  AND table_schema NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')
                ORDER BY table_schema, table_name
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<SqlTableInfo> results = new();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SqlTableInfo(
                    Schema: reader.GetString(0),
                    Name: reader.GetString(1)
                ));
            }

            return results;
        }

        public override async ValueTask<SqlTableSchemaInfo> GetTableSchemaAsync(
            string table,
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            schema = await ResolveSchemaAsync(schema, cancellationToken);

            return await CollectTableSchemaAsync(
                table, schema,
                GetColumnsAsync, GetConstraintsAsync, GetIndexesAsync,
                cancellationToken);
        }

        public override async ValueTask<SqlTableStats> GetTableStatsAsync(
            string table,
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            schema = await ResolveSchemaAsync(schema, cancellationToken);

            const string sql = """
                SELECT
                    table_rows,
                    data_length + index_length
                FROM information_schema.tables
                WHERE table_schema = @schema
                  AND table_name = @table
                  AND table_type = 'BASE TABLE'
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException($"Table '{schema}.{table}' not found.");

            long rowCount = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0));
            long totalBytes = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1));

            return new SqlTableStats(
                Schema: schema,
                Name: table,
                EstimatedRowCount: rowCount,
                TotalBytes: totalBytes,
                TotalSizePretty: FormatBytes(totalBytes)
            );
        }

        private async Task<IReadOnlyList<SqlColumnInfo>> GetColumnsAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    c.column_name,
                    c.column_type,
                    c.is_nullable,
                    c.ordinal_position,
                    c.character_maximum_length,
                    c.column_default,
                    c.column_key
                FROM information_schema.columns c
                WHERE c.table_schema = @schema
                  AND c.table_name = @table
                ORDER BY c.ordinal_position
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<SqlColumnInfo> results = new();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SqlColumnInfo(
                    Name: reader.GetString(0),
                    DataType: reader.GetString(1),
                    IsNullable: reader.GetString(2) == "YES",
                    OrdinalPosition: reader.GetInt32(3),
                    MaxLength: reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                    DefaultValue: reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsPrimaryKey: reader.GetString(6) == "PRI"
                ));
            }

            return results;
        }

        private async Task<IReadOnlyList<SqlConstraintInfo>> GetConstraintsAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    tc.constraint_name,
                    tc.constraint_type,
                    kcu.column_name,
                    kcu.ordinal_position,
                    kcu.referenced_table_schema,
                    kcu.referenced_table_name,
                    kcu.referenced_column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_schema = kcu.constraint_schema
                    AND tc.constraint_name = kcu.constraint_name
                    AND tc.table_name = kcu.table_name
                WHERE tc.table_schema = @schema
                  AND tc.table_name = @table
                ORDER BY tc.constraint_name, kcu.ordinal_position
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            Dictionary<string, (SqlConstraintType type, List<string> columns, string? refSchema, string? refTable, List<string>? refColumns)> groups = new();

            while (await reader.ReadAsync(cancellationToken))
            {
                string name = reader.GetString(0);
                string type = reader.GetString(1);
                string column = reader.GetString(2);
                string? refSchema = reader.IsDBNull(4) ? null : reader.GetString(4);
                string? refTable = reader.IsDBNull(5) ? null : reader.GetString(5);
                string? refColumn = reader.IsDBNull(6) ? null : reader.GetString(6);

                if (!groups.TryGetValue(name, out var group))
                {
                    group = (ParseConstraintType(type), new List<string>(), refSchema, refTable,
                        refTable != null ? new List<string>() : null);
                    groups[name] = group;
                }

                group.columns.Add(column);
                group.refColumns?.Add(refColumn!);
            }

            List<SqlConstraintInfo> results = new();
            foreach (var (name, (type, columns, refSchema, refTable, refColumns)) in groups)
            {
                results.Add(new SqlConstraintInfo(
                    Name: name,
                    Type: type,
                    Columns: columns,
                    RefSchema: refSchema,
                    RefTable: refTable,
                    RefColumns: refColumns
                ));
            }

            return results;
        }

        private async Task<IReadOnlyList<SqlIndexInfo>> GetIndexesAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    index_name,
                    non_unique,
                    column_name,
                    seq_in_index,
                    index_type
                FROM information_schema.statistics
                WHERE table_schema = @schema
                  AND table_name = @table
                ORDER BY index_name, seq_in_index
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            Dictionary<string, (bool isUnique, string method, List<string> columns)> groups = new();

            while (await reader.ReadAsync(cancellationToken))
            {
                string indexName = reader.GetString(0);
                bool nonUnique = Convert.ToInt32(reader.GetValue(1)) != 0;
                string column = reader.GetString(2);
                string indexType = reader.GetString(4);

                if (!groups.TryGetValue(indexName, out var group))
                {
                    group = (!nonUnique, indexType.ToLowerInvariant(), new List<string>());
                    groups[indexName] = group;
                }

                group.columns.Add(column);
            }

            List<SqlIndexInfo> results = new();
            foreach (var (name, (isUnique, method, columns)) in groups)
            {
                results.Add(new SqlIndexInfo(
                    Name: name,
                    Method: method,
                    IsUnique: isUnique,
                    IsPrimary: name == "PRIMARY",
                    Columns: columns
                ));
            }

            return results;
        }

        private static SqlConstraintType ParseConstraintType(string type) => type switch
        {
            "PRIMARY KEY" => SqlConstraintType.PrimaryKey,
            "FOREIGN KEY" => SqlConstraintType.ForeignKey,
            "UNIQUE" => SqlConstraintType.Unique,
            "CHECK" => SqlConstraintType.Check,
            _ => SqlConstraintType.Check
        };
    }
}
