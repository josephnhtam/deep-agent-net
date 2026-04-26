using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Internal;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Internal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Tools.SqlDatabaseTools
{
    public class SqlDatabaseProvider : AIContextProvider
    {
        private readonly SqlContextProviderOptions _options;
        private readonly IReadOnlyList<AITool> _tools;

        public SqlDatabaseProvider(SqlContextProviderOptions options)
        {
            _options = options;

            SqlInspectorToolProvider inspectorToolProvider = new(
                options.Inspector,
                options.ListSchemasToolOptions,
                options.ListTablesToolOptions,
                options.GetTableSchemaToolOptions,
                options.GetTableStatsToolOptions);

            SqlExecutorToolProvider executorToolProvider = new(
                options.Executor,
                options.IsReadOnly,
                options.FileSystemAccess,
                options.QuerySqlToolOptions,
                options.ExecuteSqlToolOptions,
                options.ExportSqlCsvToolOptions);

            _tools = [.. inspectorToolProvider.Tools, .. executorToolProvider.Tools];
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(
            InvokingContext context, CancellationToken cancellationToken = default)
        {
            SqlDatabaseInfo info = new(_options.Inspector.Dialect);

            return new(new AIContext
            {
                Instructions = _options.SystemPrompt?.Invoke(info) ?? SqlDatabaseDefaults.GetSystemPrompt(info),
                Tools = _tools,
            });
        }
    }
}
