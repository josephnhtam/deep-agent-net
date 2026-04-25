using DeepAgentNet.Shared;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Contracts;

namespace DeepAgentNet.Tools.SqlDatabaseTools
{
    public record SqlQueryToolOptions : ToolOptions
    {
        public int? MaxRows { get; init; }
    }

    public record SqlContextProviderOptions(
        ISqlInspector Inspector,
        ISqlExecutor Executor)
    {
        public Func<SqlDatabaseInfo, string>? SystemPrompt { get; init; }
        public bool IsReadOnly { get; init; } = true;
        public SqlQueryToolOptions QuerySqlToolOptions { get; init; } = new();
        public SqlQueryToolOptions ExecuteSqlToolOptions { get; init; } = new() { ApprovalPolicy = ToolApprovalPolicy.Required };
        public ToolOptions ListSchemasToolOptions { get; init; } = new();
        public ToolOptions ListTablesToolOptions { get; init; } = new();
        public ToolOptions GetTableSchemaToolOptions { get; init; } = new();
        public ToolOptions GetTableStatsToolOptions { get; init; } = new();
    }
}
