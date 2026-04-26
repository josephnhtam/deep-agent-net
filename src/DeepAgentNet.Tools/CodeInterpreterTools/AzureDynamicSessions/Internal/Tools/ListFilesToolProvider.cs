using DeepAgentNet.Shared;
using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients;
using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients.Contracts;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Tools
{
    internal class ListFilesToolProvider
    {
        private readonly IAzureDynamicSessionsClient _client;
        private readonly string _sessionId;

        public AITool Tool { get; }

        public ListFilesToolProvider(
            IAzureDynamicSessionsClient client,
            string sessionId,
            ToolOptions? options = null)
        {
            _client = client;
            _sessionId = sessionId;

            options ??= new();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = AzureDynamicSessionsDefaults.ListFilesToolName,
                Description = options.Description ?? AzureDynamicSessionsDefaults.ListFilesToolDescription,
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<IReadOnlyList<SessionFile>> ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            return await _client.ListFilesAsync(_sessionId, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
