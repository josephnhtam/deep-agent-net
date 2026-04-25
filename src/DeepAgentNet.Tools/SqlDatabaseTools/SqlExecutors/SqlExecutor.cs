using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors
{
    public abstract class SqlExecutor : ISqlExecutor
    {
        private readonly Func<DbConnection> _connectionFactory;
        private readonly SqlExecutorOptions _options;
        private readonly ISqlInterceptor? _interceptor;

        protected SqlExecutor(Func<DbConnection> connectionFactory, SqlExecutorOptions? options)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _options = options ?? new();
            _interceptor = _options.Interceptor;
        }

        private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            DbConnection connection = _connectionFactory();
            await connection.OpenAsync(cancellationToken);
            return connection;
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
            sql = _interceptor?.Intercept(sql, readOnly) ?? sql;

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

            List<string> columns = ReadColumns(reader);
            List<IReadOnlyList<object?>> rows = new();

            await foreach (var values in ReadRowsAsync(reader, maxRows, linkedToken).ConfigureAwait(false))
            {
                rows.Add(values);
            }

            return new SqlQueryResult(
                Columns: columns,
                Rows: rows,
                RowCount: rows.Count,
                AffectedRows: reader.RecordsAffected
            );
        }

        public async IAsyncEnumerable<SqlRow> ExecuteRowsAsync(
            string sql,
            IReadOnlyDictionary<string, object?>? parameters = null,
            int? maxRows = null,
            bool readOnly = false,
            TimeSpan? timeout = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            sql = _interceptor?.Intercept(sql, readOnly) ?? sql;

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

            List<string> columns = ReadColumns(reader);

            await foreach (var values in ReadRowsAsync(reader, maxRows, linkedToken).ConfigureAwait(false))
            {
                yield return new SqlRow(columns, values);
            }
        }

        private static List<string> ReadColumns(DbDataReader reader)
        {
            List<string> columns = new(reader.FieldCount);

            for (int i = 0; i < reader.FieldCount; i++)
                columns.Add(reader.GetName(i));

            return columns;
        }

        private static async IAsyncEnumerable<object?[]> ReadRowsAsync(
            DbDataReader reader, int? maxRows,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            int rowCount = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                if (maxRows.HasValue && rowCount >= maxRows.Value)
                    break;

                object?[] values = new object?[reader.FieldCount];

                for (int i = 0; i < reader.FieldCount; i++)
                    values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                yield return values;

                rowCount++;
            }
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

        protected abstract ValueTask<DbTransaction?> BeginReadOnlyTransactionAsync(
            DbConnection conn, CancellationToken cancellationToken);
    }
}
