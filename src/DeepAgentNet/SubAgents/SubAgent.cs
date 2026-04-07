using DeepAgentNet.SubAgents.Contracts;

namespace DeepAgentNet.SubAgents
{
    public record SubAgent(string Name, string? Description, ISubAgentHandle Handle, ISubAgentFactory Factory);
}
