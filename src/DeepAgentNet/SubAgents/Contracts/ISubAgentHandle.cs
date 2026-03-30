using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents.Contracts
{
    public interface ISubAgentHandle
    {
        Task<FunctionApprovalResponseContent> ApproveFunctionCallAsync(FunctionApprovalRequestContent call, CancellationToken cancellationToken);
        Task<object?> ProvideFunctionResultAsync(FunctionCallContent call, CancellationToken cancellationToken);
    }
}
