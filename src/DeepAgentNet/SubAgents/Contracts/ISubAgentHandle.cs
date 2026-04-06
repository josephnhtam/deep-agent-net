using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents.Contracts
{
    public interface ISubAgentHandle
    {
        Task<ToolApprovalResponseContent> ApproveToolCallAsync(string agentId, ToolApprovalRequestContent call, CancellationToken cancellationToken);
        Task<object?> ProvideFunctionResultAsync(string agentId, FunctionCallContent call, CancellationToken cancellationToken);
        ValueTask ReceiveUpdateAsync(string agentId, AgentResponseUpdate update, CancellationToken cancellationToken);
        ValueTask ReceiveResponseAsync(string agentId, AgentResponse response, CancellationToken cancellationToken);
        ValueTask OnSessionCreatedAsync(string agentId, string taskId, bool resumed, CancellationToken cancellationToken) => default;
    }
}
