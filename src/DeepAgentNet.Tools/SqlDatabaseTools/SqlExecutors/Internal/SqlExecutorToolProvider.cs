using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Internal
{
    internal class SqlExecutorToolProvider
    {
        private readonly ISqlExecutor _executor;
        private readonly IFileSystemAccess? _fileSystemAccess;
        private readonly int? _queryMaxRows;
        private readonly int? _executeMaxRows;
        private readonly int? _exportMaxRows;

        public IReadOnlyList<AITool> Tools { get; }

        public SqlExecutorToolProvider(ISqlExecutor executor,
            bool isReadOnly = true,
            IFileSystemAccess? fileSystemAccess = null,
            SqlQueryToolOptions? querySqlToolOptions = null,
            SqlQueryToolOptions? executeSqlToolOptions = null,
            SqlQueryToolOptions? exportSqlCsvToolOptions = null)
        {
            _executor = executor;
            _fileSystemAccess = fileSystemAccess;

            querySqlToolOptions ??= new();
            executeSqlToolOptions ??= new();
            exportSqlCsvToolOptions ??= new();

            _queryMaxRows = querySqlToolOptions.MaxRows;
            _executeMaxRows = executeSqlToolOptions.MaxRows;
            _exportMaxRows = exportSqlCsvToolOptions.MaxRows;

            List<AITool> tools =
            [
                CreateTool(QuerySqlAsync, SqlDatabaseDefaults.QuerySqlToolName,
                    querySqlToolOptions.Description ?? SqlDatabaseDefaults.QuerySqlToolDescription,
                    querySqlToolOptions.ApprovalPolicy,
                    CreateQuerySchemaOptions(_queryMaxRows)),
            ];

            if (!isReadOnly)
            {
                tools.Add(CreateTool(ExecuteSqlAsync, SqlDatabaseDefaults.ExecuteSqlToolName,
                    executeSqlToolOptions.Description ?? SqlDatabaseDefaults.ExecuteSqlToolDescription,
                    executeSqlToolOptions.ApprovalPolicy,
                    CreateQuerySchemaOptions(_executeMaxRows)));
            }

            if (fileSystemAccess is not null)
            {
                tools.Add(CreateTool(ExportSqlCsvAsync, SqlDatabaseDefaults.ExportSqlCsvToolName,
                    exportSqlCsvToolOptions.Description ?? SqlDatabaseDefaults.ExportSqlCsvToolDescription,
                    exportSqlCsvToolOptions.ApprovalPolicy,
                    CreateExportSchemaOptions(_exportMaxRows, fileSystemAccess.RootWorkingDirectory)));
            }

            Tools = tools;
        }

        private static AIJsonSchemaCreateOptions CreateQuerySchemaOptions(int? maxRows) => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "maxRows" => maxRows.HasValue ?
                    $"Maximum number of rows to return in the result set (default: 100, max: {maxRows})" :
                    "Maximum number of rows to return in the result set (default: 100)",
                _ => null
            }
        };

        private static AIJsonSchemaCreateOptions CreateExportSchemaOptions(int? maxRows, string rootWorkingDirectory) => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "maxRows" => maxRows.HasValue ?
                    $"Maximum number of rows to export (max: {maxRows}). If not specified, all rows are exported." :
                    "Maximum number of rows to export. If not specified, all rows are exported.",
                "cwdPath" => $"The working directory for resolving relative paths. Defaults to '{rootWorkingDirectory}'.",
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

        private async ValueTask<SqlCsvExportResult> ExportSqlCsvAsync(
            [Description("The SQL query to execute")]
            string sql,
            [Description("The file path to write the CSV output to")]
            string filePath,
            int? maxRows = null,
            [Description("Optional timeout for the query execution")]
            TimeSpan? timeout = null,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            filePath = await _fileSystemAccess!.ResolvePathAsync(filePath, cwdPath, cancellationToken)
                .ConfigureAwait(false);

            int rowCount = 0;
            int columnCount = 0;

            await _fileSystemAccess.WriteAsync(filePath, async stream =>
            {
                IAsyncEnumerable<SqlRow> rows = _executor.ExecuteRowsAsync(
                    sql,
                    parameters: null,
                    maxRows: _exportMaxRows.HasValue && maxRows.HasValue ?
                        Math.Min(maxRows.Value, _exportMaxRows.Value) : maxRows ?? _exportMaxRows,
                    readOnly: true,
                    timeout: timeout,
                    cancellationToken: cancellationToken);

                (rowCount, columnCount) = await SqlCsvWriter.WriteAsync(stream, rows, cancellationToken)
                    .ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            return new SqlCsvExportResult(filePath, rowCount, columnCount);
        }
    }
}
