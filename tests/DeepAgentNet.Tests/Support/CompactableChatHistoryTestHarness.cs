using DeepAgentNet.Agents;
using DeepAgentNet.ChatHistories.Internal;
using DeepAgentNet.Compactions;
using DeepAgentNet.Compactions.Internal;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Tests.Support
{
    internal sealed class CompactableChatHistoryTestHarness
    {
        public RecordingChatClient Client { get; }
        public InMemoryChatHistoryProvider History { get; }
        public AIAgent Agent { get; }
        public AgentSession Session { get; }

        private CompactableChatHistoryTestHarness(
            RecordingChatClient client,
            InMemoryChatHistoryProvider history,
            AIAgent agent,
            AgentSession session)
        {
            Client = client;
            History = history;
            Agent = agent;
            Session = session;
        }

        public static CompactionProviderOptions CreateCompactionOptions(
            InMemoryChatHistoryProvider history,
            int maxGroups = 2) =>
            new(new TruncationCompactionStrategy(
                CompactionTriggers.GroupsExceed(maxGroups),
                minimumPreservedGroups: 1))
            {
                ChatHistoryProvider = history
            };

        public static async Task<CompactableChatHistoryTestHarness> CreateAsync(
            CompactionProviderOptions? compactionOptions = null,
            bool viaDeepAgent = false,
            int maxGroups = 2)
        {
            RecordingChatClient recordingClient = new();
            InMemoryChatHistoryProvider history = new();
            CompactionProviderOptions compaction =
                compactionOptions ?? CreateCompactionOptions(history, maxGroups);

            AIAgent agent;
            if (viaDeepAgent)
            {
                DeepAgentOptions deepAgentOptions = DeepAgentOptionsBuilder.Create()
                    .WithCompaction(compaction)
                    .Build();

                agent = recordingClient.AsDeepAgent(
                    CreateAgentOptions(),
                    deepAgentOptions);
            }
            else
            {
                IChatClient wrappedClient = recordingClient
                    .AsBuilder()
                    .UseCompactableChatHistory(compaction)
                    .Build();

                agent = new ChatClientAgent(wrappedClient, CreateAgentOptions());
            }

            AgentSession session = await agent.CreateSessionAsync().ConfigureAwait(false);
            return new CompactableChatHistoryTestHarness(recordingClient, history, agent, session);
        }

        private static ChatClientAgentOptions CreateAgentOptions() =>
            new()
            {
                Name = "test-agent",
                Id = Guid.NewGuid().ToString("N"),
                ChatHistoryProvider = new NoOpChatHistoryProvider(),
                ThrowOnChatHistoryProviderConflict = false,
                UseProvidedChatClientAsIs = true
            };
    }
}
