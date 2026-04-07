using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents.Contracts
{
    public interface ISubAgentHandle
    {
        Task<ToolApprovalResponseContent> ApproveToolCallAsync(string agentId, ToolApprovalRequestContent call, CancellationToken cancellationToken);
        Task<object?> ProvideFunctionResultAsync(string agentId, FunctionCallContent call, CancellationToken cancellationToken);
        ValueTask ReceiveUpdateAsync(string agentId, AgentResponseUpdate update, CancellationToken cancellationToken) => default;
        ValueTask ReceiveResponseAsync(string agentId, AgentResponse response, CancellationToken cancellationToken) => default;
        ValueTask OnSessionCreateOrResumedAsync(string agentId, string taskId, bool resumed, string description, string prompt, CancellationToken cancellationToken) => default;
        ValueTask OnSessionCompletedAsync(string agentId, string taskId, CancellationToken cancellationToken) => default;
    }
}
