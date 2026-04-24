using DeepAgentNet.Shared;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlDatabaseInspectors;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlDatabaseInspectors.Contracts;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.Tools.SqlDatabaseTools
{
    public class SqlDatabaseToolProvider
    {
        private readonly ISqlDatabaseInspector _inspector;
        private readonly ISqlExecutor _executor;

        public IReadOnlyList<AITool> Tools { get; }

        public SqlDatabaseToolProvider(
            ISqlDatabaseInspector inspector,
            ISqlExecutor executor,
            bool isReadOnly = true,
            ToolOptions? querySqlToolOptions = null,
            ToolOptions? executeSqlToolOptions = null,
            ToolOptions? listSchemasToolOptions = null,
            ToolOptions? listTablesToolOptions = null,
            ToolOptions? getTableSchemaToolOptions = null,
            ToolOptions? getTableStatsToolOptions = null)
        {
            _inspector = inspector;
            _executor = executor;

            querySqlToolOptions ??= new();
            executeSqlToolOptions ??= new();
            listSchemasToolOptions ??= new();
            listTablesToolOptions ??= new();
            getTableSchemaToolOptions ??= new();
            getTableStatsToolOptions ??= new();

            List<AITool> tools =
            [
                CreateTool(QuerySqlAsync, SqlDatabaseDefaults.QuerySqlToolName,
                    querySqlToolOptions.Description ?? SqlDatabaseDefaults.QuerySqlToolDescription,
                    querySqlToolOptions.ApprovalPolicy),

                CreateTool(ListSchemasAsync, SqlDatabaseDefaults.ListSchemasToolName,
                    listSchemasToolOptions.Description ?? SqlDatabaseDefaults.ListSchemasToolDescription,
                    listSchemasToolOptions.ApprovalPolicy),

                CreateTool(ListTablesAsync, SqlDatabaseDefaults.ListTablesToolName,
                    listTablesToolOptions.Description ?? SqlDatabaseDefaults.ListTablesToolDescription,
                    listTablesToolOptions.ApprovalPolicy),

                CreateTool(GetTableSchemaAsync, SqlDatabaseDefaults.GetTableSchemaToolName,
                    getTableSchemaToolOptions.Description ?? SqlDatabaseDefaults.GetTableSchemaToolDescription,
                    getTableSchemaToolOptions.ApprovalPolicy),

                CreateTool(GetTableStatsAsync, SqlDatabaseDefaults.GetTableStatsToolName,
                    getTableStatsToolOptions.Description ?? SqlDatabaseDefaults.GetTableStatsToolDescription,
                    getTableStatsToolOptions.ApprovalPolicy),
            ];

            if (!isReadOnly)
            {
                tools.Add(CreateTool(ExecuteSqlAsync, SqlDatabaseDefaults.ExecuteSqlToolName,
                    executeSqlToolOptions.Description ?? SqlDatabaseDefaults.ExecuteSqlToolDescription,
                    executeSqlToolOptions.ApprovalPolicy));
            }

            Tools = tools;
        }

        private static AITool CreateTool(Delegate method, string name, string description, ToolApprovalPolicy approvalPolicy)
        {
            AIFunction function = AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
            });

            return approvalPolicy == ToolApprovalPolicy.Required
                ? new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<SqlQueryResult> QuerySqlAsync(
            [Description("The SQL query to execute")]
            string sql,
            [Description("Maximum number of rows to return in the result set")]
            int maxRows = 100,
            [Description("Optional timeout for the query execution")]
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _executor.ExecuteAsync(
                sql,
                parameters: null,
                maxRows: maxRows,
                readOnly: true,
                timeout: timeout,
                cancellationToken: cancellationToken);
        }

        private async ValueTask<SqlQueryResult> ExecuteSqlAsync(
            [Description("The SQL statement to execute (INSERT, UPDATE, DELETE)")]
            string sql,
            [Description("Maximum number of rows to return in the result set")]
            int maxRows = 100,
            [Description("Optional timeout for the statement execution")]
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _executor.ExecuteAsync(
                sql,
                parameters: null,
                maxRows: maxRows,
                readOnly: false,
                timeout: timeout,
                cancellationToken: cancellationToken);
        }

        private async ValueTask<IReadOnlyList<SqlSchemaInfo>> ListSchemasAsync(
            CancellationToken cancellationToken = default)
        {
            return await _inspector.ListSchemasAsync(cancellationToken);
        }

        private async ValueTask<IReadOnlyList<SqlTableInfo>> ListTablesAsync(
            [Description("The schema to filter tables by. If not specified, tables from all schemas are returned.")]
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            return await _inspector.ListTablesAsync(schema, cancellationToken);
        }

        private async ValueTask<SqlTableSchemaInfo> GetTableSchemaAsync(
            [Description("The name of the table")] string table,
            [Description("The schema containing the table. If not specified, the default schema is used.")]
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            return await _inspector.GetTableSchemaAsync(table, schema, cancellationToken);
        }

        private async ValueTask<SqlTableStats> GetTableStatsAsync(
            [Description("The name of the table")] string table,
            [Description("The schema containing the table. If not specified, the default schema is used.")]
            string? schema = null,
            CancellationToken cancellationToken = default)
        {
            return await _inspector.GetTableStatsAsync(table, schema, cancellationToken);
        }
    }
}
