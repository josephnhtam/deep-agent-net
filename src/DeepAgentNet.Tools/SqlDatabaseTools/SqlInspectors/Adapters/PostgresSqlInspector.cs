using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Adapters
{
    public class PostgresInspector : SqlInspector
    {
        private readonly string _defaultSchema;

        public override string Dialect => "Postgres";

        public PostgresInspector(Func<DbConnection> connectionFactory, string defaultSchema = "public") :
            base(connectionFactory)
        {
            _defaultSchema = defaultSchema;
        }

        public override async ValueTask<IReadOnlyList<SqlSchemaInfo>> ListSchemasAsync(
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT schema_name, schema_owner
                FROM information_schema.schemata
                WHERE schema_name NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
                  AND schema_name NOT LIKE 'pg_temp_%'
                  AND schema_name NOT LIKE 'pg_toast_temp_%'
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
                  AND table_schema NOT IN ('pg_catalog', 'information_schema')
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
                    c.reltuples::bigint,
                    pg_total_relation_size(c.oid),
                    pg_size_pretty(pg_total_relation_size(c.oid))
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relname = @table
                  AND n.nspname = @schema
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException($"Table '{schema}.{table}' not found.");

            return new SqlTableStats(
                Schema: schema,
                Name: table,
                EstimatedRowCount: reader.GetInt64(0),
                TotalBytes: reader.GetInt64(1),
                TotalSizePretty: reader.GetString(2)
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
                    EXISTS (
                        SELECT 1
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage kcu
                            ON tc.constraint_name = kcu.constraint_name
                            AND tc.table_schema = kcu.table_schema
                        WHERE tc.table_schema = c.table_schema
                          AND tc.table_name = c.table_name
                          AND tc.constraint_type = 'PRIMARY KEY'
                          AND kcu.column_name = c.column_name
                    )
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
                    IsPrimaryKey: reader.GetBoolean(6)
                ));
            }

            return results;
        }

        private async Task<IReadOnlyList<SqlConstraintInfo>> GetConstraintsAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    con.conname,
                    con.contype::text,
                    array_to_string(ARRAY(
                        SELECT a.attname
                        FROM unnest(con.conkey) WITH ORDINALITY AS k(attnum, ord)
                        JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum
                        ORDER BY k.ord
                    ), '|'),
                    ref_ns.nspname,
                    ref_cl.relname,
                    array_to_string(ARRAY(
                        SELECT a.attname
                        FROM unnest(con.confkey) WITH ORDINALITY AS k(attnum, ord)
                        JOIN pg_attribute a ON a.attrelid = con.confrelid AND a.attnum = k.attnum
                        ORDER BY k.ord
                    ), '|')
                FROM pg_constraint con
                JOIN pg_class cl ON cl.oid = con.conrelid
                JOIN pg_namespace ns ON ns.oid = cl.relnamespace
                LEFT JOIN pg_class ref_cl ON ref_cl.oid = con.confrelid
                LEFT JOIN pg_namespace ref_ns ON ref_ns.oid = ref_cl.relnamespace
                WHERE cl.relname = @table
                  AND ns.nspname = @schema
                ORDER BY con.contype, con.conname
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<SqlConstraintInfo> results = new();
            while (await reader.ReadAsync(cancellationToken))
            {
                string? refTable = reader.IsDBNull(4) ? null : reader.GetString(4);
                string? refColumnsRaw = reader.IsDBNull(5) ? null : reader.GetString(5);

                results.Add(new SqlConstraintInfo(
                    Name: reader.GetString(0),
                    Type: ParseConstraintType(reader.GetString(1)[0]),
                    Columns: ParseDelimitedColumns(reader.GetString(2)),
                    RefSchema: reader.IsDBNull(3) ? null : reader.GetString(3),
                    RefTable: refTable,
                    RefColumns: refTable != null && !string.IsNullOrEmpty(refColumnsRaw)
                        ? ParseDelimitedColumns(refColumnsRaw)
                        : null
                ));
            }

            return results;
        }

        private async Task<IReadOnlyList<SqlIndexInfo>> GetIndexesAsync(
            string table, string schema, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    i.relname,
                    am.amname,
                    ix.indisunique,
                    ix.indisprimary,
                    array_to_string(ARRAY(
                        SELECT a.attname
                        FROM unnest(ix.indkey) WITH ORDINALITY AS k(attnum, ord)
                        JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
                        ORDER BY k.ord
                    ), '|')
                FROM pg_class t
                JOIN pg_index ix ON t.oid = ix.indrelid
                JOIN pg_class i ON i.oid = ix.indexrelid
                JOIN pg_am am ON i.relam = am.oid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE t.relname = @table
                  AND n.nspname = @schema
                ORDER BY i.relname
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(CreateParameter(cmd, "schema", schema));
            cmd.Parameters.Add(CreateParameter(cmd, "table", table));

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<SqlIndexInfo> results = new();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SqlIndexInfo(
                    Name: reader.GetString(0),
                    Method: reader.GetString(1),
                    IsUnique: reader.GetBoolean(2),
                    IsPrimary: reader.GetBoolean(3),
                    Columns: ParseDelimitedColumns(reader.GetString(4))
                ));
            }

            return results;
        }

        private static List<string> ParseDelimitedColumns(string value) =>
            string.IsNullOrEmpty(value) ? [] : value.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

        private static SqlConstraintType ParseConstraintType(char c) => c switch
        {
            'p' => SqlConstraintType.PrimaryKey,
            'f' => SqlConstraintType.ForeignKey,
            'u' => SqlConstraintType.Unique,
            'c' => SqlConstraintType.Check,
            'x' => SqlConstraintType.Exclude,
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };
    }
}
