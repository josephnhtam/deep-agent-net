using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Adapters
{
    public class SpannerInspector : SqlInspector
    {
        public override string Dialect => "Spanner";

        public SpannerInspector(Func<DbConnection> connectionFactory) :
            base(connectionFactory)
        {
        }

        public override ValueTask<IReadOnlyList<SqlSchemaInfo>> ListSchemasAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SqlSchemaInfo> schemas = [new SqlSchemaInfo(Name: "", Owner: "")];
            return new(schemas);
        }

        public override async ValueTask<IReadOnlyList<SqlTableInfo>> ListTablesAsync(
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                  AND table_schema = ''
                ORDER BY table_name
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;

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
            schema ??= "";

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
            schema ??= "";

            const string sql = """
                SELECT
                    row_count,
                    bytes
                FROM spanner_sys.table_sizes
                WHERE table_name = @table
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException($"Table '{table}' not found.");

            long rowCount = reader.GetInt64(0);
            long totalBytes = reader.GetInt64(1);

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
                    c.spanner_type,
                    c.is_nullable,
                    c.ordinal_position
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
            List<string> pkColumns = await GetPrimaryKeyColumnsAsync(table, schema, cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                string columnName = reader.GetString(0);
                string spannerType = reader.GetString(1);

                results.Add(new SqlColumnInfo(
                    Name: columnName,
                    DataType: spannerType,
                    IsNullable: reader.GetString(2) == "YES",
                    OrdinalPosition: Convert.ToInt32(reader.GetValue(3)),
                    MaxLength: ParseMaxLength(spannerType),
                    DefaultValue: null,
                    IsPrimaryKey: pkColumns.Contains(columnName)
                ));
            }

            return results;
        }

        private async Task<List<string>> GetPrimaryKeyColumnsAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT kcu.column_name
                FROM information_schema.key_column_usage kcu
                JOIN information_schema.table_constraints tc
                    ON kcu.constraint_name = tc.constraint_name
                    AND kcu.table_schema = tc.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY'
                  AND kcu.table_schema = @schema
                  AND kcu.table_name = @table
                ORDER BY kcu.ordinal_position
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<string> columns = new();
            while (await reader.ReadAsync(cancellationToken))
                columns.Add(reader.GetString(0));

            return columns;
        }

        private async Task<IReadOnlyList<SqlConstraintInfo>> GetConstraintsAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    tc.constraint_name,
                    tc.constraint_type,
                    kcu.column_name,
                    kcu.ordinal_position
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name
                    AND tc.table_schema = kcu.table_schema
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

            Dictionary<string, (SqlConstraintType type, List<string> columns)> groups = new();

            while (await reader.ReadAsync(cancellationToken))
            {
                string name = reader.GetString(0);
                string type = reader.GetString(1);
                string column = reader.GetString(2);

                if (!groups.TryGetValue(name, out var group))
                {
                    group = (ParseConstraintType(type), new List<string>());
                    groups[name] = group;
                }

                group.columns.Add(column);
            }

            List<SqlConstraintInfo> results = new();

            List<SqlConstraintInfo> fkConstraints = await GetForeignKeyConstraintsAsync(table, schema, cancellationToken);

            foreach (var (name, (type, columns)) in groups)
            {
                results.Add(new SqlConstraintInfo(
                    Name: name,
                    Type: type,
                    Columns: columns
                ));
            }

            results.AddRange(fkConstraints);

            return results;
        }

        private async Task<List<SqlConstraintInfo>> GetForeignKeyConstraintsAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    rc.constraint_name,
                    kcu.column_name,
                    ccu.table_schema AS ref_schema,
                    ccu.table_name AS ref_table,
                    ccu.column_name AS ref_column
                FROM information_schema.referential_constraints rc
                JOIN information_schema.key_column_usage kcu
                    ON rc.constraint_name = kcu.constraint_name
                    AND rc.constraint_schema = kcu.constraint_schema
                JOIN information_schema.constraint_column_usage ccu
                    ON rc.unique_constraint_name = ccu.constraint_name
                    AND rc.unique_constraint_schema = ccu.constraint_schema
                WHERE kcu.table_schema = @schema
                  AND kcu.table_name = @table
                ORDER BY rc.constraint_name, kcu.ordinal_position
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            Dictionary<string, (string refSchema, string refTable, List<string> columns, List<string> refColumns)> groups = new();

            while (await reader.ReadAsync(cancellationToken))
            {
                string name = reader.GetString(0);
                string column = reader.GetString(1);
                string refSchema = reader.GetString(2);
                string refTable = reader.GetString(3);
                string refColumn = reader.GetString(4);

                if (!groups.TryGetValue(name, out var group))
                {
                    group = (refSchema, refTable, new List<string>(), new List<string>());
                    groups[name] = group;
                }

                group.columns.Add(column);
                group.refColumns.Add(refColumn);
            }

            List<SqlConstraintInfo> results = new();
            foreach (var (name, (refSchema, refTable, columns, refColumns)) in groups)
            {
                results.Add(new SqlConstraintInfo(
                    Name: name,
                    Type: SqlConstraintType.ForeignKey,
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
                    i.index_name,
                    i.index_type,
                    i.is_unique,
                    ic.column_name,
                    ic.ordinal_position
                FROM information_schema.indexes i
                JOIN information_schema.index_columns ic
                    ON i.table_schema = ic.table_schema
                    AND i.table_name = ic.table_name
                    AND i.index_name = ic.index_name
                WHERE i.table_schema = @schema
                  AND i.table_name = @table
                ORDER BY i.index_name, ic.ordinal_position
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            Dictionary<string, (string method, bool isUnique, List<string> columns)> groups = new();

            while (await reader.ReadAsync(cancellationToken))
            {
                string indexName = reader.GetString(0);
                string indexType = reader.GetString(1);
                bool isUnique = reader.GetBoolean(2);
                string column = reader.GetString(3);

                if (!groups.TryGetValue(indexName, out var group))
                {
                    group = (indexType.ToLowerInvariant(), isUnique, new List<string>());
                    groups[indexName] = group;
                }

                group.columns.Add(column);
            }

            List<SqlIndexInfo> results = new();
            foreach (var (name, (method, isUnique, columns)) in groups)
            {
                results.Add(new SqlIndexInfo(
                    Name: name,
                    Method: method,
                    IsUnique: isUnique,
                    IsPrimary: name == "PRIMARY_KEY",
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

        private static int? ParseMaxLength(string spannerType)
        {
            int start = spannerType.IndexOf('(');
            int end = spannerType.IndexOf(')');

            if (start < 0 || end < 0 || end <= start + 1)
                return null;

            string lengthStr = spannerType.Substring(start + 1, end - start - 1);

            if (lengthStr.Equals("MAX", StringComparison.OrdinalIgnoreCase))
                return null;

            return int.TryParse(lengthStr, out int length) ? length : null;
        }
    }
}
