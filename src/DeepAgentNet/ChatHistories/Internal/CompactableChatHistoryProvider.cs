using DeepAgentNet.Compactions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DeepAgentNet.ChatHistories.Internal
{
    internal class CompactableChatHistoryProvider : ChatHistoryProvider
    {
        private readonly CompactionProviderOptions _options;
        private readonly ChatHistoryProvider _inner;
        private readonly ILogger? _logger;
        private readonly ProviderSessionState<CompactionState> _compactionSessionState;

        private const string CompactionTriggeredKey = "CompactionTriggered";
        private const string KeyCompactionMessageId = "CompactionMessageId";

        public CompactableChatHistoryProvider(CompactionProviderOptions options, ChatHistoryProvider inner)
        {
            _options = options;
            _inner = inner;
            _logger = options.LoggerFactory?.CreateLogger<CompactableChatHistoryProvider>();

            _compactionSessionState = new(_ => new CompactionState(),
                options.CompactionStateKey ?? nameof(CompactionState), AIJsonUtilities.DefaultOptions);
        }

        protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
            InvokingContext context, CancellationToken cancellationToken = default)
        {
            TriggeredSessionState.SaveState(context.Session, new CompactionTriggeredState(false));

            List<ChatMessage> originalMessages = [.. await _inner.InvokingAsync(context, cancellationToken)];
            List<ChatMessage> messages = originalMessages;

            CompactionState state = _compactionSessionState.GetOrInitializeState(context.Session);

            if (state.LastIndex.HasValue)
            {
                messages = [.. state.Messages, .. messages.Skip(state.LastIndex.Value)];
            }

            foreach (ChatMessage message in messages)
            {
                message.AdditionalProperties ??= new();
                if (!message.AdditionalProperties.TryGetValue(KeyCompactionMessageId, out string? id) ||
                    string.IsNullOrEmpty(id))
                {
                    message.AdditionalProperties[KeyCompactionMessageId] = Guid.NewGuid().ToString("N");
                }
            }

            Dictionary<string, int> messageIndexes = originalMessages
                .Select((m, i) => (Message: m, Index: i))
                .Where(x => x.Message.AdditionalProperties?.ContainsKey(KeyCompactionMessageId) == true)
                .ToDictionary(x => x.Message.AdditionalProperties![KeyCompactionMessageId]!.ToString()!,
                    x => x.Index);

            List<ChatMessage> compactedMessages =
            [
                .. await CompactionProvider.CompactAsync(
                    _options.CompactionStrategy, messages, _logger, cancellationToken)
            ];

            if (compactedMessages.Count < messages.Count)
            {
                TriggeredSessionState.SaveState(context.Session, new CompactionTriggeredState(true));
            }

            bool foundOriginal = false;
            for (int ci = 0; ci < compactedMessages.Count; ci++)
            {
                ChatMessage message = compactedMessages[ci];

                if (message.AdditionalProperties != null &&
                    message.AdditionalProperties.TryGetValue(KeyCompactionMessageId, out string? id) &&
                    !string.IsNullOrEmpty(id) && messageIndexes.TryGetValue(id, out int originalIndex))
                {
                    state.Messages = compactedMessages[..ci];
                    state.LastIndex = originalIndex;
                    foundOriginal = true;
                    break;
                }
            }

            if (!foundOriginal)
            {
                state.Messages = compactedMessages;
                state.LastIndex = originalMessages.Count;
            }

            state.OriginalRequestMessages = originalMessages;

            _compactionSessionState.SaveState(context.Session, state);
            return compactedMessages;
        }

        protected override ValueTask<IEnumerable<ChatMessage>> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            return ProvideChatHistoryAsync(context, cancellationToken);
        }

        protected override ValueTask StoreChatHistoryAsync(
            InvokedContext context, CancellationToken cancellationToken = default)
        {
            CompactionState state = _compactionSessionState.GetOrInitializeState(context.Session);

            if (state.OriginalRequestMessages is not null)
            {
                context = new InvokedContext(
                    context.Agent, context.Session,
                    state.OriginalRequestMessages, context.ResponseMessages ?? []);

                state.OriginalRequestMessages = null;
                _compactionSessionState.SaveState(context.Session, state);
            }

            return _inner.InvokedAsync(context, cancellationToken);
        }

        internal static readonly ProviderSessionState<CompactionTriggeredState> TriggeredSessionState =
            new(_ => new CompactionTriggeredState(false), CompactionTriggeredKey, AIJsonUtilities.DefaultOptions);

        internal record CompactionTriggeredState(bool Triggered);

        private class CompactionState
        {
            public int? LastIndex { get; set; }
            public List<ChatMessage> Messages { get; set; } = new();
            public List<ChatMessage>? OriginalRequestMessages { get; set; }
        }
    }
}
