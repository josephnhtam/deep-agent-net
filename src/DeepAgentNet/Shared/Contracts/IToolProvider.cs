using Microsoft.Extensions.AI;

namespace DeepAgentNet.Shared.Contracts
{
    public interface IToolProvider
    {
        AITool Tool { get; }
    }
}
