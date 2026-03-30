using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents.Contracts
{
    public interface ISubAgentHandle
    {
        Task<FunctionApprovalResponseContent> ApproveFunctionCallAsync(string agentId, FunctionApprovalRequestContent call, CancellationToken cancellationToken);
        Task<object?> ProvideFunctionResultAsync(string agentId, FunctionCallContent call, CancellationToken cancellationToken);
        ValueTask ReceiveUpdateAsync(string agentId, AgentResponseUpdate update, CancellationToken cancellationToken);
        ValueTask ReceiveResponseAsync(string agentId, AgentResponse response, CancellationToken cancellationToken);
    }
}
