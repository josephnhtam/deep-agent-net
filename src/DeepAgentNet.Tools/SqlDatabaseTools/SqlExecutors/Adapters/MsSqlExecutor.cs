using System.Data;
using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Adapters
{
    public class MsSqlExecutor : SqlExecutor
    {
        public MsSqlExecutor(Func<DbConnection> connectionFactory, SqlExecutorOptions? options = null) :
            base(connectionFactory, options)
        {
        }

        protected override async ValueTask<DbTransaction?> BeginReadOnlyTransactionAsync(
            DbConnection conn, CancellationToken cancellationToken)
        {
            try
            {
                return await conn.BeginTransactionAsync(IsolationLevel.Snapshot, cancellationToken);
            }
            catch (Exception)
            {
                return await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            }
        }
    }
}
