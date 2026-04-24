using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts;
using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors
{
    public abstract class SqlExecutor : ISqlExecutor
    {
        private readonly Func<DbConnection> _connectionFactory;
        private readonly SqlExecutorOptions _options;

        protected SqlExecutor(Func<DbConnection> connectionFactory, SqlExecutorOptions? options)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _options = options ?? new();
        }

        private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            DbConnection connection = _connectionFactory();
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        private static void BindParameters(DbCommand cmd, IReadOnlyDictionary<string, object?>? parameters)
        {
            if (parameters is null)
                return;

            foreach (KeyValuePair<string, object?> kvp in parameters)
            {
                DbParameter param = cmd.CreateParameter();
                param.ParameterName = kvp.Key;
                param.Value = kvp.Value ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        }

        private CancellationTokenSource CreateTimeoutCancellationTokenSource(
            TimeSpan? timeout, CancellationToken token)
        {
            timeout ??= _options.DefaultTimeout;

            if (!timeout.HasValue)
                return CancellationTokenSource.CreateLinkedTokenSource(token);

            CancellationTokenSource cts = new CancellationTokenSource(timeout.Value);
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);
            return linkedCts;
        }

        public async ValueTask<SqlQueryResult> ExecuteAsync(
            string sql,
            IReadOnlyDictionary<string, object?>? parameters = null,
            int maxRows = 1000,
            bool readOnly = false,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource linkedCts = CreateTimeoutCancellationTokenSource(timeout, cancellationToken);
            CancellationToken linkedToken = linkedCts.Token;

            await using DbConnection conn = await OpenConnectionAsync(linkedToken);

            await using DbTransaction? tx = readOnly ?
                await BeginReadOnlyTransactionAsync(conn, linkedToken) : null;

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = tx;
            BindParameters(cmd, parameters);

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(linkedToken);

            List<string> columns = new(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            List<IReadOnlyList<object?>> rows = new();
            int rowCount = 0;

            while (await reader.ReadAsync(linkedToken))
            {
                if (rowCount >= maxRows)
                    break;

                object?[] values = new object?[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(values);
                rowCount++;
            }

            SqlQueryResult result = new(
                Columns: columns,
                Rows: rows,
                RowCount: rowCount,
                AffectedRows: reader.RecordsAffected
            );

            return result;
        }

        protected abstract ValueTask<DbTransaction?> BeginReadOnlyTransactionAsync(
            DbConnection conn, CancellationToken cancellationToken);
    }
}
