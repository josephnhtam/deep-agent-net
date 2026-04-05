using DeepAgentNet.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace DeepAgentNet.Compactions.Internal
{
    public class CompactionChatClient : DelegatingChatClient
    {
        private readonly CompactionProvider _compactionProvider;

        internal CompactionChatClient(IChatClient innerClient, CompactionProviderOptions providerOptions) : base(innerClient)
        {
            _compactionProvider = new CompactionProvider(
                providerOptions.CompactionStrategy, providerOptions.CompactionStateKey, providerOptions.LoggerFactory);
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            (messages, options) = await ProcessCompactionAsync(messages, options, cancellationToken);
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            (messages, options) = await ProcessCompactionAsync(messages, options, cancellationToken);

            await foreach (ChatResponseUpdate update in
                base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }
        }

        private async Task<(IEnumerable<ChatMessage> messages, ChatOptions? options)> ProcessCompactionAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
        {
            if (options?.AdditionalProperties?.TryGetValue(DeepAgent.KeyAgent, out var agentValue) != true ||
                agentValue is not AIAgent agent)
            {
                return (messages, options);
            }

            if (options?.AdditionalProperties?.TryGetValue(DeepAgent.KeySession, out var sessionValue) != true ||
                sessionValue is not AgentSession session)
            {
                return (messages, options);
            }

            AIContextProvider.InvokingContext compactionInvokingContext = new(
                agent, session, new AIContext
                {
                    Instructions = options.Instructions,
                    Tools = options.Tools,
                    Messages = messages
                });

            AIContext aiContext = await _compactionProvider
                .InvokingAsync(compactionInvokingContext, cancellationToken)
                .ConfigureAwait(false);

            messages = aiContext.Messages ?? [];

            var tools = aiContext.Tools as IList<AITool> ?? aiContext.Tools?.ToList();
            if (options?.Tools is { Count: > 0 } || tools is { Count: > 0 })
            {
                options ??= new();
                options.Tools = tools;
            }

            if (options?.Instructions is not null || aiContext.Instructions is not null)
            {
                options ??= new();
                options.Instructions = aiContext.Instructions;
            }

            return (messages, options);
        }
    }
}
