using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace DeepAgentNet.ChatHistories.Internal
{
    internal class ChatHistoryChatClient : DelegatingChatClient
    {
        private readonly ChatHistoryProvider _chatHistoryProvider;

        private const string LocalHistoryConversationId = "deep-agent-net-local-history";

        public ChatHistoryChatClient(IChatClient innerClient, ChatHistoryProvider chatHistoryProvider) : base(innerClient)
        {
            _chatHistoryProvider = chatHistoryProvider;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            (AIAgent agent, AgentSession session) = GetRequiredAgentAndSession();
            options = StripLocalHistoryConversationId(options);

            ChatHistoryProvider.InvokingContext invokingContext = new ChatHistoryProvider.InvokingContext(agent, session, messages);
            IEnumerable<ChatMessage> fullMessages = await _chatHistoryProvider.InvokingAsync(invokingContext, cancellationToken);

            List<ChatMessage> requestMessages = [.. fullMessages];
            ChatResponse response = await base.GetResponseAsync(requestMessages, options, cancellationToken);

            await NotifyProviderAsync(agent, session, requestMessages, response.Messages, cancellationToken);

            response.ConversationId = LocalHistoryConversationId;
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            (AIAgent agent, AgentSession session) = GetRequiredAgentAndSession();
            options = StripLocalHistoryConversationId(options);

            ChatHistoryProvider.InvokingContext invokingContext = new ChatHistoryProvider.InvokingContext(agent, session, messages);
            IEnumerable<ChatMessage> fullMessages = await _chatHistoryProvider.InvokingAsync(invokingContext, cancellationToken);

            List<ChatMessage> requestMessages = [.. fullMessages];
            List<ChatResponseUpdate> updates = [];

            IAsyncEnumerable<ChatResponseUpdate> streamingResponse =
                base.GetStreamingResponseAsync(requestMessages, options, cancellationToken);

            IAsyncEnumerator<ChatResponseUpdate> enumerator = streamingResponse.GetAsyncEnumerator(cancellationToken);
            await using var _ = enumerator.ConfigureAwait(false);

            bool hasUpdates;
            try
            {
                hasUpdates = await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await NotifyProviderOfFailureAsync(agent, session, ex, requestMessages, cancellationToken).ConfigureAwait(false);
                throw;
            }

            while (hasUpdates)
            {
                ChatResponseUpdate update = enumerator.Current;
                update.ConversationId = LocalHistoryConversationId;
                updates.Add(update);

                yield return update.Clone();

                try
                {
                    hasUpdates = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await NotifyProviderOfFailureAsync(agent, session, ex, requestMessages, cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }

            ChatResponse response = updates.ToChatResponse();
            await NotifyProviderAsync(agent, session, requestMessages, response.Messages, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask NotifyProviderAsync(
            AIAgent agent, AgentSession session,
            IEnumerable<ChatMessage> requestMessages,
            IEnumerable<ChatMessage> responseMessages,
            CancellationToken cancellationToken)
        {
            ChatHistoryProvider.InvokedContext invokedContext = new(agent, session, requestMessages, responseMessages);
            await _chatHistoryProvider.InvokedAsync(invokedContext, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask NotifyProviderOfFailureAsync(
            AIAgent agent, AgentSession session,
            Exception exception,
            IEnumerable<ChatMessage> requestMessages,
            CancellationToken cancellationToken)
        {
            ChatHistoryProvider.InvokedContext invokedContext = new(agent, session, requestMessages, exception);
            await _chatHistoryProvider.InvokedAsync(invokedContext, cancellationToken).ConfigureAwait(false);
        }

        private static (AIAgent Agent, AgentSession Session) GetRequiredAgentAndSession()
        {
            var context = AIAgent.CurrentRunContext
                ?? throw new InvalidOperationException($"{nameof(ChatHistoryChatClient)} requires an active agent run context.");

            var session = context.Session
                ?? throw new InvalidOperationException($"{nameof(ChatHistoryChatClient)} requires a session in the agent run context.");

            return (context.Agent, session);
        }

        private static ChatOptions? StripLocalHistoryConversationId(ChatOptions? options)
        {
            if (options?.ConversationId != LocalHistoryConversationId)
                return options;

            var cloned = options.Clone();
            cloned.ConversationId = null;
            return cloned;
        }
    }
}
