using Microsoft.Extensions.AI;

namespace DeepAgentNet.Compactions.Internal
{
    internal static class CompactionChatClientExtensions
    {
        public static CompactionChatClient AsCompactionChatClient(this IChatClient chatClient, CompactionProviderOptions providerOptions)
        {
            if (chatClient.GetService<CompactionChatClient>() is { } inner)
            {
                return inner;
            }

            return new CompactionChatClient(chatClient, providerOptions);
        }
    }
}
