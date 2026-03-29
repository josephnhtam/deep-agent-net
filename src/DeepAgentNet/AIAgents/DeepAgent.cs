using Microsoft.Agents.AI;

namespace DeepAgentNet.AIAgents
{
    public class DeepAgent : DelegatingAIAgent
    {
        public DeepAgent(AIAgent innerAgent) : base(innerAgent)
        {
        }
    }
}
