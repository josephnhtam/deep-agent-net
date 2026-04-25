namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts
{
    public interface ISqlExecutor
    {
        ValueTask<SqlQueryResult> ExecuteAsync(
            string sql,
            IReadOnlyDictionary<string, object?>? parameters = null,
            int maxRows = 100,
            bool readOnly = false,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<SqlRow> ExecuteRowsAsync(
            string sql,
            IReadOnlyDictionary<string, object?>? parameters = null,
            int? maxRows = null,
            bool readOnly = false,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }
}
