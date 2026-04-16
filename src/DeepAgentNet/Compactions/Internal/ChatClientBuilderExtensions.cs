using DeepAgentNet.ChatHistories.Internal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Compactions.Internal
{
    internal static class ChatClientBuilderExtensions
    {
        public static ChatClientBuilder UseCompactableChatHistory(
            this ChatClientBuilder builder, CompactionProviderOptions providerOptions)
        {
            CompactableChatHistoryProvider compactableChatHistoryProvider =
                new(providerOptions, providerOptions.ChatHistoryProvider ?? new InMemoryChatHistoryProvider());

            return builder.Use(inner => new ChatHistoryChatClient(inner, compactableChatHistoryProvider));
        }
    }
}
