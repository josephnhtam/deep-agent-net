using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Tools.SqlDatabaseTools
{
    public class SqlDatabaseContextProvider : AIContextProvider
    {
        private readonly SqlContextProviderOptions _options;
        private readonly IReadOnlyList<AITool> _tools;

        public SqlDatabaseContextProvider(SqlContextProviderOptions options)
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
                options.QuerySqlToolOptions,
                options.ExecuteSqlToolOptions);

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
