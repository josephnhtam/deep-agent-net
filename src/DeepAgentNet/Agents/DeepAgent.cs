using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Agents
{
    public class DeepAgent : DelegatingAIAgent
    {
        public const string KeyAgent = "DeepAgent.Agent";
        public const string KeySession = "DeepAgent.Session";

        public DeepAgent(AIAgent innerAgent) : base(innerAgent)
        {
        }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages, AgentSession? session = null,
            AgentRunOptions? options = null, CancellationToken cancellationToken = default)
        {
            ProvideSessionContext(session, ref options);
            return base.RunCoreAsync(messages, session, options, cancellationToken);
        }

        protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages, AgentSession? session = null,
            AgentRunOptions? options = null, CancellationToken cancellationToken = default)
        {
            ProvideSessionContext(session, ref options);
            return base.RunCoreStreamingAsync(messages, session, options, cancellationToken);
        }

        private void ProvideSessionContext(AgentSession? session, ref AgentRunOptions? options)
        {
            options ??= new();
            options.AdditionalProperties ??= new();
            options.AdditionalProperties[KeyAgent] = this;
            options.AdditionalProperties[KeySession] = session;
        }
    }
}
