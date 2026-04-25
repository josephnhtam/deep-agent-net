using DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Contracts;
using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors
{
    public abstract class SqlInspector : ISqlInspector
    {
        private readonly Func<DbConnection> _connectionFactory;

        public abstract string Dialect { get; }

        protected SqlInspector(Func<DbConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        protected async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            DbConnection connection = _connectionFactory();
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        protected static DbParameter CreateParameter(DbCommand cmd, string name, object? value)
        {
            DbParameter param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }

        public abstract ValueTask<IReadOnlyList<SqlSchemaInfo>> ListSchemasAsync(
            CancellationToken cancellationToken = default);

        public abstract ValueTask<IReadOnlyList<SqlTableInfo>> ListTablesAsync(
            string? schema = null,
            CancellationToken cancellationToken = default);

        public ValueTask<SqlTableSchemaInfo> GetTableSchemaAsync(
            SqlTableInfo table,
            CancellationToken cancellationToken = default)
        {
            return GetTableSchemaAsync(table.Name, table.Schema, cancellationToken);
        }

        public abstract ValueTask<SqlTableSchemaInfo> GetTableSchemaAsync(
            string table,
            string? schema = null,
            CancellationToken cancellationToken = default);

        public abstract ValueTask<SqlTableStats> GetTableStatsAsync(
            string table,
            string? schema = null,
            CancellationToken cancellationToken = default);

        protected async ValueTask<SqlTableSchemaInfo> CollectTableSchemaAsync(
            string table,
            string schema,
            Func<string, string, CancellationToken, Task<IReadOnlyList<SqlColumnInfo>>> getColumns,
            Func<string, string, CancellationToken, Task<IReadOnlyList<SqlConstraintInfo>>> getConstraints,
            Func<string, string, CancellationToken, Task<IReadOnlyList<SqlIndexInfo>>> getIndexes,
            CancellationToken cancellationToken)
        {
            Task<IReadOnlyList<SqlColumnInfo>> columnsTask = getColumns(table, schema, cancellationToken);
            Task<IReadOnlyList<SqlConstraintInfo>> constraintsTask = getConstraints(table, schema, cancellationToken);
            Task<IReadOnlyList<SqlIndexInfo>> indexesTask = getIndexes(table, schema, cancellationToken);

            await Task.WhenAll(columnsTask, constraintsTask, indexesTask);

            return new SqlTableSchemaInfo(
                Schema: schema,
                Name: table,
                Columns: await columnsTask,
                Constraints: await constraintsTask,
                Indexes: await indexesTask
            );
        }

        protected static string FormatBytes(long bytes) => bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} kB",
            _ => $"{bytes} bytes"
        };
    }
}
