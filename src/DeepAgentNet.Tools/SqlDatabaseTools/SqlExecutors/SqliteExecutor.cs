using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors
{
    public class SqliteExecutor : SqlExecutor
    {
        public SqliteExecutor(Func<DbConnection> connectionFactory, SqlExecutorOptions? options = null) :
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
