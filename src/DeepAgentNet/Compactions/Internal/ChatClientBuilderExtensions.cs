using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Compactions.Internal
{
    internal static class ChatClientBuilderExtensions
    {
        public static ChatClientBuilder UseCompactionProvider(this ChatClientBuilder builder, CompactionProviderOptions providerOptions) =>
            builder.UseAIContextProviders(new CompactionProvider(providerOptions.CompactionStrategy, providerOptions.CompactionStateKey, providerOptions.LoggerFactory));
    }
}
