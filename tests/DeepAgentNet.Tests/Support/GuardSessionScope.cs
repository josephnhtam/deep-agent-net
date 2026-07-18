using System.Reflection;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Tests.Support
{
    internal sealed class GuardSessionScope : IAsyncDisposable
    {
        private static readonly MethodInfo SetCurrentRunContext =
            typeof(AIAgent).GetProperty(
                nameof(AIAgent.CurrentRunContext),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException("AIAgent.CurrentRunContext setter not found.");

        private readonly AgentRunContext? _previous;
        private readonly ChatClientAgent _agent;

        public AgentSession Session { get; }
        public AIAgent Agent => _agent;

        private GuardSessionScope(ChatClientAgent agent, AgentSession session, AgentRunContext? previous)
        {
            _agent = agent;
            Session = session;
            _previous = previous;
        }

        public static async Task<(ChatClientAgent Agent, AgentSession Session)> CreateSessionAsync()
        {
            var agent = new ChatClientAgent(new StubChatClient(), new ChatClientAgentOptions
            {
                Name = "test-agent",
                Id = Guid.NewGuid().ToString("N")
            });

            AgentSession session = await agent.CreateSessionAsync().ConfigureAwait(false);
            return (agent, session);
        }

        public static GuardSessionScope Enter(ChatClientAgent agent, AgentSession session)
        {
            AgentRunContext? previous = AIAgent.CurrentRunContext;
            var runContext = new AgentRunContext(
                agent,
                session,
                Array.Empty<ChatMessage>(),
                new AgentRunOptions());

            SetCurrentRunContext.Invoke(null, [runContext]);

            if (AIAgent.CurrentRunContext?.Session is null)
                throw new InvalidOperationException("Failed to establish AIAgent.CurrentRunContext for guard tests.");

            return new GuardSessionScope(agent, session, previous);
        }

        public ValueTask DisposeAsync()
        {
            SetCurrentRunContext.Invoke(null, [_previous]);
            return ValueTask.CompletedTask;
        }

        private sealed class StubChatClient : IChatClient
        {
            public ChatClientMetadata Metadata { get; } = new("stub");

            public void Dispose() { }

            public Task<ChatResponse> GetResponseAsync(
                IEnumerable<ChatMessage> chatMessages,
                ChatOptions? options = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));

            public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
                IEnumerable<ChatMessage> chatMessages,
                ChatOptions? options = null,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
                await Task.CompletedTask;
            }

            public object? GetService(Type serviceType, object? serviceKey = null) => null;
        }
    }
}
