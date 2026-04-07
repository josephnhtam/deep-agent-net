using DeepAgentNet.Agents.Internal;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace DeepAgentNet.Compactions.Internal
{
    public class CompactionChatClient : DelegatingChatClient
    {
        private readonly CompactionProvider _compactionProvider;
        public CompactionProviderOptions ProviderOptions { get; }

        internal CompactionChatClient(IChatClient innerClient, CompactionProviderOptions providerOptions) : base(innerClient)
        {
            ProviderOptions = providerOptions;

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
            if (options is null)
                return (messages, options);

            AIAgent? agent = options.GetAgent();
            AgentSession? session = options.GetSession();

            if (agent is null || session is null)
                return (messages, options);

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
            if (options.Tools is { Count: > 0 } || tools is { Count: > 0 })
            {
                options.Tools = tools;
            }

            if (options.Instructions is not null || aiContext.Instructions is not null)
            {
                options.Instructions = aiContext.Instructions;
            }

            return (messages, options);
        }
    }
}
