using DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Contracts;
using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Adapters
{
    public class SqliteInspector : ISqlInspector
    {
        private readonly Func<DbConnection> _connectionFactory;

        public string Dialect => "SQLite";

        public SqliteInspector(Func<DbConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            DbConnection connection = _connectionFactory();
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        public ValueTask<IReadOnlyList<SqlSchemaInfo>> ListSchemasAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SqlSchemaInfo> schemas = [new SqlSchemaInfo(Name: "main", Owner: "")];
            return new(schemas);
        }

        public async ValueTask<IReadOnlyList<SqlTableInfo>> ListTablesAsync(
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT name
                FROM sqlite_master
                WHERE type = 'table'
                  AND name NOT LIKE 'sqlite_%'
                ORDER BY name
                """;

            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<SqlTableInfo> results = new();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SqlTableInfo(
                    Schema: "main",
                    Name: reader.GetString(0)
                ));
            }

            return results;
        }

        public ValueTask<SqlTableSchemaInfo> GetTableSchemaAsync(
            SqlTableInfo table,
            CancellationToken cancellationToken = default)
        {
            return GetTableSchemaAsync(table.Name, table.Schema, cancellationToken);
        }

        public async ValueTask<SqlTableSchemaInfo> GetTableSchemaAsync(
            string table,
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            IReadOnlyList<SqlColumnInfo> columns = await GetColumnsAsync(conn, table, cancellationToken);
            if (columns.Count == 0)
                throw new InvalidOperationException($"Table '{table}' not found.");

            IReadOnlyList<SqlConstraintInfo> constraints = await GetConstraintsAsync(conn, table, columns, cancellationToken);
            IReadOnlyList<SqlIndexInfo> indexes = await GetIndexesAsync(conn, table, cancellationToken);

            return new SqlTableSchemaInfo(
                Schema: "main",
                Name: table,
                Columns: columns,
                Constraints: constraints,
                Indexes: indexes
            );
        }

        public async ValueTask<SqlTableStats> GetTableStatsAsync(
            string table,
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            await using DbConnection conn = await OpenConnectionAsync(cancellationToken);

            await using DbCommand countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM \"{EscapeIdentifier(table)}\"";
            long rowCount = (long)(await countCmd.ExecuteScalarAsync(cancellationToken))!;

            await using DbCommand pageSizeCmd = conn.CreateCommand();
            pageSizeCmd.CommandText = "PRAGMA page_size";
            long pageSize = Convert.ToInt64(await pageSizeCmd.ExecuteScalarAsync(cancellationToken));

            await using DbCommand pageCountCmd = conn.CreateCommand();
            pageCountCmd.CommandText = "PRAGMA page_count";
            long pageCount = Convert.ToInt64(await pageCountCmd.ExecuteScalarAsync(cancellationToken));

            long totalBytes = pageSize * pageCount;

            return new SqlTableStats(
                Schema: "main",
                Name: table,
                EstimatedRowCount: rowCount,
                TotalBytes: totalBytes,
                TotalSizePretty: FormatBytes(totalBytes)
            );
        }

        private static async Task<IReadOnlyList<SqlColumnInfo>> GetColumnsAsync(
            DbConnection conn, string table, CancellationToken cancellationToken)
        {
            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{EscapeIdentifier(table)}\")";

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<SqlColumnInfo> results = new();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SqlColumnInfo(
                    Name: reader.GetString(1),
                    DataType: reader.GetString(2),
                    IsNullable: reader.GetInt32(3) == 0,
                    IsPrimaryKey: reader.GetInt32(5) > 0,
                    OrdinalPosition: reader.GetInt32(0) + 1,
                    MaxLength: null,
                    DefaultValue: reader.IsDBNull(4) ? null : reader.GetString(4)
                ));
            }

            return results;
        }

        private static async Task<IReadOnlyList<SqlConstraintInfo>> GetConstraintsAsync(
            DbConnection conn, string table, IReadOnlyList<SqlColumnInfo> columns,
            CancellationToken cancellationToken)
        {
            List<SqlConstraintInfo> results = new();

            List<string> pkColumns = columns
                .Where(c => c.IsPrimaryKey)
                .OrderBy(c => c.OrdinalPosition)
                .Select(c => c.Name)
                .ToList();

            if (pkColumns.Count > 0)
            {
                results.Add(new SqlConstraintInfo(
                    Name: $"pk_{table}",
                    Type: SqlConstraintType.PrimaryKey,
                    Columns: pkColumns
                ));
            }

            await using DbCommand fkCmd = conn.CreateCommand();
            fkCmd.CommandText = $"PRAGMA foreign_key_list(\"{EscapeIdentifier(table)}\")";

            await using DbDataReader fkReader = await fkCmd.ExecuteReaderAsync(cancellationToken);

            Dictionary<int, (string refTable, List<string> columns, List<string> refColumns)> fkGroups = new();
            while (await fkReader.ReadAsync(cancellationToken))
            {
                int id = fkReader.GetInt32(0);
                string refTable = fkReader.GetString(2);
                string from = fkReader.GetString(3);
                string to = fkReader.GetString(4);

                if (!fkGroups.TryGetValue(id, out var group))
                {
                    group = (refTable, new List<string>(), new List<string>());
                    fkGroups[id] = group;
                }

                group.columns.Add(from);
                group.refColumns.Add(to);
            }

            foreach (var (id, (refTable, fkCols, refCols)) in fkGroups.OrderBy(g => g.Key))
            {
                results.Add(new SqlConstraintInfo(
                    Name: $"fk_{table}_{id}",
                    Type: SqlConstraintType.ForeignKey,
                    Columns: fkCols,
                    RefSchema: "main",
                    RefTable: refTable,
                    RefColumns: refCols
                ));
            }

            await using DbCommand idxCmd = conn.CreateCommand();
            idxCmd.CommandText = $"PRAGMA index_list(\"{EscapeIdentifier(table)}\")";

            await using DbDataReader idxReader = await idxCmd.ExecuteReaderAsync(cancellationToken);

            List<(string name, string origin)> uniqueIndexes = new();
            while (await idxReader.ReadAsync(cancellationToken))
            {
                string origin = idxReader.GetString(3);
                if (origin != "u")
                    continue;

                uniqueIndexes.Add((idxReader.GetString(1), origin));
            }

            foreach (var (indexName, _) in uniqueIndexes)
            {
                List<string> uniqueCols = await GetIndexColumnsAsync(conn, indexName, cancellationToken);

                results.Add(new SqlConstraintInfo(
                    Name: indexName,
                    Type: SqlConstraintType.Unique,
                    Columns: uniqueCols
                ));
            }

            return results;
        }

        private static async Task<IReadOnlyList<SqlIndexInfo>> GetIndexesAsync(
            DbConnection conn, string table, CancellationToken cancellationToken)
        {
            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA index_list(\"{EscapeIdentifier(table)}\")";

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<(string name, bool isUnique, string origin)> rawIndexes = new();
            while (await reader.ReadAsync(cancellationToken))
            {
                rawIndexes.Add((
                    name: reader.GetString(1),
                    isUnique: reader.GetInt32(2) != 0,
                    origin: reader.GetString(3)
                ));
            }

            List<SqlIndexInfo> results = new();
            foreach (var (name, isUnique, origin) in rawIndexes)
            {
                List<string> indexColumns = await GetIndexColumnsAsync(conn, name, cancellationToken);

                results.Add(new SqlIndexInfo(
                    Name: name,
                    Method: "btree",
                    IsUnique: isUnique,
                    IsPrimary: origin == "pk",
                    Columns: indexColumns
                ));
            }

            return results;
        }

        private static async Task<List<string>> GetIndexColumnsAsync(
            DbConnection conn, string indexName, CancellationToken cancellationToken)
        {
            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA index_info(\"{EscapeIdentifier(indexName)}\")";

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

            List<string> columns = new();
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(2));
            }

            return columns;
        }

        private static string EscapeIdentifier(string identifier) =>
            identifier.Replace("\"", "\"\"");

        private static string FormatBytes(long bytes) => bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} kB",
            _ => $"{bytes} bytes"
        };
    }
}
