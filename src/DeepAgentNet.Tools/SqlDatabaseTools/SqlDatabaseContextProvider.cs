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

            SqlDatabaseToolProvider toolProvider = new(
                options.Inspector,
                options.Executor,
                options.IsReadOnly,
                options.QuerySqlToolOptions,
                options.ExecuteSqlToolOptions,
                options.ListSchemasToolOptions,
                options.ListTablesToolOptions,
                options.GetTableSchemaToolOptions,
                options.GetTableStatsToolOptions);

            _tools = toolProvider.Tools;
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(
            InvokingContext context, CancellationToken cancellationToken = default)
        {
            return new(new AIContext
            {
                Instructions = _options.SystemPrompt ??
                    SqlDatabaseDefaults.GetSystemPrompt(_options.Inspector.Dialect),
                Tools = _tools,
            });
        }
    }
}
