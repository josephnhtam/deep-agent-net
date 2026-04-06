using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Agents.Internal
{
    internal static class ChatOptionsExtensions
    {
        public static AgentSession? GetSession(this ChatOptions? options)
        {
            if (options?.AdditionalProperties?.TryGetValue(DeepAgent.KeySession, out var value) == true &&
                value is AgentSession session)
            {
                return session;
            }

            return null;
        }

        public static AIAgent? GetAgent(this ChatOptions? options)
        {
            if (options?.AdditionalProperties?.TryGetValue(DeepAgent.KeyAgent, out var value) == true &&
                value is AIAgent agent)
            {
                return agent;
            }

            return null;
        }
    }
}
