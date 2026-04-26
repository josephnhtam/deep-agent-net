using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Adapters
{
    public class MsSqlInspector : SqlInspector
    {
        private readonly string _defaultSchema;

        public override string Dialect => "SqlServer";

        public MsSqlInspector(Func<DbConnection> connectionFactory, string defaultSchema = "dbo") :
            base(connectionFactory)
        {
            _defaultSchema = defaultSchema;
        }

        public override async ValueTask<IReadOnlyList<SqlSchemaInfo>> ListSchemasAsync(
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT s.name, dp.name AS owner
                FROM sys.schemas s
                JOIN sys.database_principals dp ON s.principal_id = dp.principal_id
                WHERE s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest')
                  AND s.name NOT LIKE 'db[_]%'
                ORDER BY s.name
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
            schema ??= _defaultSchema;

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
            schema ??= _defaultSchema;

            const string sql = """
                SELECT
                    SUM(p.rows),
                    SUM(a.total_pages) * 8 * 1024
                FROM sys.tables t
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                JOIN sys.indexes i ON t.object_id = i.object_id
                JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
                JOIN sys.allocation_units a ON p.partition_id = a.container_id
                WHERE s.name = @schema
                  AND t.name = @table
                  AND i.index_id IN (0, 1)
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
                throw new InvalidOperationException($"Table '{schema}.{table}' not found.");

            long rowCount = Convert.ToInt64(reader.GetValue(0));
            long totalBytes = Convert.ToInt64(reader.GetValue(1));

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
                    c.data_type,
                    c.is_nullable,
                    c.ordinal_position,
                    c.character_maximum_length,
                    c.column_default,
                    CASE WHEN EXISTS (
                        SELECT 1
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage kcu
                            ON tc.constraint_name = kcu.constraint_name
                            AND tc.table_schema = kcu.table_schema
                        WHERE tc.table_schema = c.table_schema
                          AND tc.table_name = c.table_name
                          AND tc.constraint_type = 'PRIMARY KEY'
                          AND kcu.column_name = c.column_name
                    ) THEN 1 ELSE 0 END
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
                    MaxLength: reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    DefaultValue: reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsPrimaryKey: Convert.ToInt32(reader.GetValue(6)) == 1
                ));
            }

            return results;
        }

        private async Task<IReadOnlyList<SqlConstraintInfo>> GetConstraintsAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string constraintsSql = """
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
                  AND tc.constraint_type IN ('PRIMARY KEY', 'UNIQUE')
                ORDER BY tc.constraint_name, kcu.ordinal_position
                """;

            const string fkSql = """
                SELECT
                    fk.name AS constraint_name,
                    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS column_name,
                    SCHEMA_NAME(rt.schema_id) AS ref_schema,
                    rt.name AS ref_table,
                    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ref_column
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                JOIN sys.tables t ON fk.parent_object_id = t.object_id
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
                WHERE s.name = @schema
                  AND t.name = @table
                ORDER BY fk.name, fkc.constraint_column_id
                """;

            const string checkSql = """
                SELECT
                    cc.name AS constraint_name
                FROM sys.check_constraints cc
                JOIN sys.tables t ON cc.parent_object_id = t.object_id
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = @schema
                  AND t.name = @table
                ORDER BY cc.name
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            List<SqlConstraintInfo> results = new();

            {
                await using DbCommand cmd = conn.CreateCommand();
                cmd.CommandText = constraintsSql;
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
                        group = (type == "PRIMARY KEY" ? SqlConstraintType.PrimaryKey : SqlConstraintType.Unique,
                            new List<string>());
                        groups[name] = group;
                    }

                    group.columns.Add(column);
                }

                foreach (var (name, (type, columns)) in groups)
                {
                    results.Add(new SqlConstraintInfo(
                        Name: name,
                        Type: type,
                        Columns: columns
                    ));
                }
            }

            {
                await using DbCommand cmd = conn.CreateCommand();
                cmd.CommandText = fkSql;
                cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
                cmd.Parameters.Add(CreateParameter(cmd, "table", table));

                await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

                Dictionary<string, (string refSchema, string refTable, List<string> columns, List<string> refColumns)> fkGroups = new();

                while (await reader.ReadAsync(cancellationToken))
                {
                    string name = reader.GetString(0);
                    string column = reader.GetString(1);
                    string refSchemaVal = reader.GetString(2);
                    string refTable = reader.GetString(3);
                    string refColumn = reader.GetString(4);

                    if (!fkGroups.TryGetValue(name, out var group))
                    {
                        group = (refSchemaVal, refTable, new List<string>(), new List<string>());
                        fkGroups[name] = group;
                    }

                    group.columns.Add(column);
                    group.refColumns.Add(refColumn);
                }

                foreach (var (name, (refSchema, refTable, columns, refColumns)) in fkGroups)
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
            }

            {
                await using DbCommand cmd = conn.CreateCommand();
                cmd.CommandText = checkSql;
                cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
                cmd.Parameters.Add(CreateParameter(cmd, "table", table));

                await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new SqlConstraintInfo(
                        Name: reader.GetString(0),
                        Type: SqlConstraintType.Check,
                        Columns: []
                    ));
                }
            }

            return results;
        }

        private async Task<IReadOnlyList<SqlIndexInfo>> GetIndexesAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    i.name,
                    i.type_desc,
                    i.is_unique,
                    i.is_primary_key,
                    COL_NAME(ic.object_id, ic.column_id) AS column_name
                FROM sys.indexes i
                JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                JOIN sys.tables t ON i.object_id = t.object_id
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = @schema
                  AND t.name = @table
                  AND i.name IS NOT NULL
                  AND ic.is_included_column = 0
                ORDER BY i.name, ic.key_ordinal
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            Dictionary<string, (string method, bool isUnique, bool isPrimary, List<string> columns)> groups = new();

            while (await reader.ReadAsync(cancellationToken))
            {
                string name = reader.GetString(0);
                string typeDesc = reader.GetString(1);
                bool isUnique = reader.GetBoolean(2);
                bool isPrimary = reader.GetBoolean(3);
                string column = reader.GetString(4);

                if (!groups.TryGetValue(name, out var group))
                {
                    group = (typeDesc.ToLowerInvariant(), isUnique, isPrimary, new List<string>());
                    groups[name] = group;
                }

                group.columns.Add(column);
            }

            List<SqlIndexInfo> results = new();
            foreach (var (name, (method, isUnique, isPrimary, columns)) in groups)
            {
                results.Add(new SqlIndexInfo(
                    Name: name,
                    Method: method,
                    IsUnique: isUnique,
                    IsPrimary: isPrimary,
                    Columns: columns
                ));
            }

            return results;
        }
    }
}
