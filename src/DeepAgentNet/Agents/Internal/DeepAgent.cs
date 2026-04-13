using Microsoft.Agents.AI;

namespace DeepAgentNet.Agents.Internal
{
    internal class DeepAgent : DelegatingAIAgent
    {
        internal DeepAgent(AIAgent innerAgent) : base(innerAgent) { }
    }
}
