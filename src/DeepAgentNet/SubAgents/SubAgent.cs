using DeepAgentNet.SubAgents.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace DeepAgentNet.SubAgents
{
    public delegate ValueTask<AIAgent> SubAgentFactory(
        SubAgentDefaultOptions defaultOptions,
        ILoggerFactory? loggerFactory,
        IServiceProvider? services,
        CancellationToken cancellationToken);

    public record SubAgent(string Name, string? Description, ISubAgentHandle Handle, SubAgentFactory Factory);
}
