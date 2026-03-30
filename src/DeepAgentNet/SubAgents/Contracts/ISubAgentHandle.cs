using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents.Contracts
{
    public interface ISubAgentHandle
    {
        Task<FunctionApprovalResponseContent> ApproveFunctionCallAsync(FunctionApprovalRequestContent call, CancellationToken cancellationToken);
        Task<object?> ProvideFunctionResultAsync(FunctionCallContent call, CancellationToken cancellationToken);
        ValueTask ReceiveUpdateAsync(AgentResponseUpdate update, CancellationToken cancellationToken);
        ValueTask ReceiveResponseAsync(AgentResponse response, CancellationToken cancellationToken);
    }
}
