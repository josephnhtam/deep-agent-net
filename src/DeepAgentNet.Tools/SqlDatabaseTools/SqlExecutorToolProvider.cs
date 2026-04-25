using DeepAgentNet.Shared;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.Tools.SqlDatabaseTools
{
    public class SqlExecutorToolProvider
    {
        private readonly ISqlExecutor _executor;
        private readonly int? _queryMaxRows;
        private readonly int? _executeMaxRows;

        public IReadOnlyList<AITool> Tools { get; }

        public SqlExecutorToolProvider(
            ISqlExecutor executor,
            bool isReadOnly = true,
            SqlQueryToolOptions? querySqlToolOptions = null,
            SqlQueryToolOptions? executeSqlToolOptions = null)
        {
            _executor = executor;

            querySqlToolOptions ??= new();
            executeSqlToolOptions ??= new();

            _queryMaxRows = querySqlToolOptions.MaxRows;
            _executeMaxRows = executeSqlToolOptions.MaxRows;

            List<AITool> tools =
            [
                CreateTool(QuerySqlAsync, SqlDatabaseDefaults.QuerySqlToolName,
                    querySqlToolOptions.Description ?? SqlDatabaseDefaults.QuerySqlToolDescription,
                    querySqlToolOptions.ApprovalPolicy,
                    CreateSchemaOptions(_queryMaxRows)),
            ];

            if (!isReadOnly)
            {
                tools.Add(CreateTool(ExecuteSqlAsync, SqlDatabaseDefaults.ExecuteSqlToolName,
                    executeSqlToolOptions.Description ?? SqlDatabaseDefaults.ExecuteSqlToolDescription,
                    executeSqlToolOptions.ApprovalPolicy,
                    CreateSchemaOptions(_executeMaxRows)));
            }

            Tools = tools;
        }

        private static AIJsonSchemaCreateOptions CreateSchemaOptions(int? maxRows) => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "maxRows" => maxRows.HasValue ?
                    $"Maximum number of rows to return in the result set (default: 100, max: {maxRows})" :
                    "Maximum number of rows to return in the result set (default: 100)",
                _ => null
            }
        };

        private static AITool CreateTool(
            Delegate method, string name, string description,
            ToolApprovalPolicy approvalPolicy, AIJsonSchemaCreateOptions? schemaOptions = null)
        {
            AIFunction function = AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
                JsonSchemaCreateOptions = schemaOptions,
            });

            return approvalPolicy == ToolApprovalPolicy.Required ? new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<SqlQueryResult> QuerySqlAsync(
            [Description("The SQL query to execute")]
            string sql,
            int maxRows = 100,
            [Description("Optional timeout for the query execution")]
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _executor.ExecuteAsync(
                sql,
                parameters: null,
                maxRows: _queryMaxRows.HasValue ? Math.Min(maxRows, _queryMaxRows.Value) : maxRows,
                readOnly: true,
                timeout: timeout,
                cancellationToken: cancellationToken);
        }

        private async ValueTask<SqlQueryResult> ExecuteSqlAsync(
            [Description("The SQL statement to execute (INSERT, UPDATE, DELETE)")]
            string sql,
            int maxRows = 100,
            [Description("Optional timeout for the statement execution")]
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _executor.ExecuteAsync(
                sql,
                parameters: null,
                maxRows: _executeMaxRows.HasValue ? Math.Min(maxRows, _executeMaxRows.Value) : maxRows,
                readOnly: false,
                timeout: timeout,
                cancellationToken: cancellationToken);
        }
    }
}
