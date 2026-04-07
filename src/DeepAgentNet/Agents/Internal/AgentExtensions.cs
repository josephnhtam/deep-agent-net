using Microsoft.Agents.AI;

namespace DeepAgentNet.Agents.Internal
{
    internal static class AgentExtensions
    {
        public static DeepAgent AsDeepAgent(this AIAgent agent)
        {
            if (agent.GetService<DeepAgent>() is { } inner)
            {
                return inner;
            }

            return new DeepAgent(agent);
        }
    }
}
