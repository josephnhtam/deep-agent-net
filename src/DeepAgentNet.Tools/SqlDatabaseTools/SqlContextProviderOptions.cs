using DeepAgentNet.Shared;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Contracts;

namespace DeepAgentNet.Tools.SqlDatabaseTools
{
    public record SqlContextProviderOptions(
        ISqlInspector Inspector,
        ISqlExecutor Executor)
    {
        public string? SystemPrompt { get; init; }
        public bool IsReadOnly { get; init; } = true;
        public ToolOptions QuerySqlToolOptions { get; init; } = new();
        public ToolOptions ExecuteSqlToolOptions { get; init; } = new();
        public ToolOptions ListSchemasToolOptions { get; init; } = new();
        public ToolOptions ListTablesToolOptions { get; init; } = new();
        public ToolOptions GetTableSchemaToolOptions { get; init; } = new();
        public ToolOptions GetTableStatsToolOptions { get; init; } = new();
    }
}
