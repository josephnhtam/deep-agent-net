using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Adapters
{
    public class SpannerSqlExecutor : SqlExecutor
    {
        public SpannerSqlExecutor(Func<DbConnection> connectionFactory, SqlExecutorOptions? options = null) :
            base(connectionFactory, options)
        {
        }

        protected override ValueTask<DbTransaction?> BeginReadOnlyTransactionAsync(
            DbConnection conn, CancellationToken cancellationToken)
        {
            return new((DbTransaction?)null);
        }
    }
}
