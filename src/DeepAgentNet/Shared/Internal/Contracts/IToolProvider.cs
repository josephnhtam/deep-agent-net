using Microsoft.Extensions.AI;

namespace DeepAgentNet.Shared.Internal.Contracts
{
    internal interface IToolProvider
    {
        AITool Tool { get; }
    }
}
