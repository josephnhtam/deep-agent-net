using DeepAgentNet.Shared;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.Tools.SqlDatabaseTools
{
    public class SqlInspectorToolProvider
    {
        private readonly ISqlInspector _inspector;

        public IReadOnlyList<AITool> Tools { get; }

        public SqlInspectorToolProvider(
            ISqlInspector inspector,
            ToolOptions? listSchemasToolOptions = null,
            ToolOptions? listTablesToolOptions = null,
            ToolOptions? getTableSchemaToolOptions = null,
            ToolOptions? getTableStatsToolOptions = null)
        {
            _inspector = inspector;

            listSchemasToolOptions ??= new();
            listTablesToolOptions ??= new();
            getTableSchemaToolOptions ??= new();
            getTableStatsToolOptions ??= new();

            Tools =
            [
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
