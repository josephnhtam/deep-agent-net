using System.Data;
using System.Data.Common;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors
{
    public class PostgresSqlExecutor : SqlExecutor
    {
        public PostgresSqlExecutor(Func<DbConnection> connectionFactory, SqlExecutorOptions? options = null) :
            base(connectionFactory, options)
        {
        }

        protected override async ValueTask<DbTransaction?> BeginReadOnlyTransactionAsync(
            DbConnection conn, CancellationToken cancellationToken)
        {
            DbTransaction tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            await using DbCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SET TRANSACTION READ ONLY";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            return tx;
        }
    }
}
