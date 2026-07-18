using DeepAgentNet.Compactions;
using DeepAgentNet.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace DeepAgentNet.Tests.Integration
{
    public class CompactableChatHistoryIntegrationTests
    {
        private const string LocalHistoryConversationId = "deep-agent-net-local-history";

        [Fact]
        public async Task RunAsync_Should_PersistTurnsInHistory_When_CompactionNotTriggered()
        {
            CompactableChatHistoryTestHarness harness = await CompactableChatHistoryTestHarness.CreateAsync(maxGroups: 10);

            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-1")], harness.Session);
            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-2")], harness.Session);

            List<ChatMessage> stored = harness.History.GetMessages(harness.Session);
            Assert.Equal(4, stored.Count);
            Assert.False(harness.Session.IsCompactionTriggered());
            Assert.Equal(2, harness.Client.Invocations.Count);
            Assert.Single(harness.Client.Invocations[0].Messages);
            Assert.Equal(3, harness.Client.Invocations[1].Messages.Count);
        }

        [Fact]
        public async Task RunAsync_Should_SendCompactedMessagesToInnerClient_When_HistoryExceedsTrigger()
        {
            CompactableChatHistoryTestHarness harness = await CompactableChatHistoryTestHarness.CreateAsync();

            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-1")], harness.Session);
            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-2")], harness.Session);

            List<ChatMessage> storedAfterTurn2 = harness.History.GetMessages(harness.Session);
            Assert.True(harness.Session.IsCompactionTriggered());
            Assert.True(harness.Client.Invocations[1].Messages.Count < storedAfterTurn2.Count);

            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-3")], harness.Session);

            List<ChatMessage> stored = harness.History.GetMessages(harness.Session);
            RecordingChatClientInvocation lastInvocation = harness.Client.Invocations[^1];

            Assert.True(stored.Count > lastInvocation.Messages.Count);
            Assert.True(harness.Session.IsCompactionTriggered());
        }

        [Fact]
        public async Task RunAsync_Should_StoreFullRequestInInnerProvider_When_CompactionRewritesOutboundMessages()
        {
            CompactableChatHistoryTestHarness harness = await CompactableChatHistoryTestHarness.CreateAsync();

            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-1")], harness.Session);
            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-2")], harness.Session);
            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-3")], harness.Session);

            List<ChatMessage> storedAfterTurn3 = harness.History.GetMessages(harness.Session);
            Assert.True(harness.Client.Invocations[2].Messages.Count < storedAfterTurn3.Count);

            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-4")], harness.Session);

            List<ChatMessage> stored = harness.History.GetMessages(harness.Session);
            Assert.Equal(8, stored.Count);
            Assert.Equal(4, stored.Count(m => m.Role == ChatRole.User));
            Assert.Equal(4, stored.Count(m => m.Role == ChatRole.Assistant));
            AssertContainsUserTexts(stored, "turn-1", "turn-2", "turn-3", "turn-4");
        }

        [Fact]
        public async Task RunAsync_Should_StripLocalConversationId_When_OptionsUseLocalHistoryId()
        {
            CompactableChatHistoryTestHarness harness = await CompactableChatHistoryTestHarness.CreateAsync(maxGroups: 10);

            AgentResponse response = await harness.Agent.RunAsync(
                [new ChatMessage(ChatRole.User, "turn-1")],
                harness.Session,
                new ChatClientAgentRunOptions(new ChatOptions { ConversationId = LocalHistoryConversationId }));

            Assert.Null(harness.Client.Invocations[0].Options?.ConversationId);
            Assert.Equal(LocalHistoryConversationId, (response.RawRepresentation as ChatResponse)?.ConversationId);
        }

        [Fact]
        public async Task RunStreamingAsync_Should_StripLocalConversationId_When_OptionsUseLocalHistoryId()
        {
            CompactableChatHistoryTestHarness harness = await CompactableChatHistoryTestHarness.CreateAsync(maxGroups: 10);

            List<AgentResponseUpdate> updates = [];
            await foreach (AgentResponseUpdate update in harness.Agent.RunStreamingAsync(
                               [new ChatMessage(ChatRole.User, "turn-1")],
                               harness.Session,
                               new ChatClientAgentRunOptions(new ChatOptions { ConversationId = LocalHistoryConversationId })))
            {
                updates.Add(update);
            }

            Assert.NotEmpty(updates);
            Assert.Null(harness.Client.Invocations[0].Options?.ConversationId);
            Assert.Contains(
                updates,
                u => (u.RawRepresentation as ChatResponseUpdate)?.ConversationId == LocalHistoryConversationId);
        }

        [Fact]
        public async Task RunStreamingAsync_Should_PersistAndCompact_When_HistoryExceedsTrigger()
        {
            CompactableChatHistoryTestHarness harness = await CompactableChatHistoryTestHarness.CreateAsync();

            await RunStreamingTurnAsync(harness, "turn-1");
            await RunStreamingTurnAsync(harness, "turn-2");
            await RunStreamingTurnAsync(harness, "turn-3");

            List<ChatMessage> stored = harness.History.GetMessages(harness.Session);
            RecordingChatClientInvocation lastInvocation = harness.Client.Invocations[^1];

            Assert.Equal(6, stored.Count);
            Assert.True(stored.Count > lastInvocation.Messages.Count);
            Assert.True(harness.Session.IsCompactionTriggered());
        }

        [Fact]
        public async Task AsDeepAgent_Should_ConstructAndRunWithCompaction_When_OnlyCompactionEnabled()
        {
            CompactableChatHistoryTestHarness harness =
                await CompactableChatHistoryTestHarness.CreateAsync(viaDeepAgent: true);

            await RunTurnsAsync(harness, 3);

            List<ChatMessage> stored = harness.History.GetMessages(harness.Session);
            RecordingChatClientInvocation lastInvocation = harness.Client.Invocations[^1];

            Assert.Equal(3, harness.Client.Invocations.Count);
            Assert.True(harness.Session.IsCompactionTriggered());
            Assert.True(stored.Count > lastInvocation.Messages.Count);
        }

        [Fact]
        public async Task AsDeepAgent_Should_CompactHistory_When_DefaultGroupsExceedTrigger()
        {
            CompactableChatHistoryTestHarness harness =
                await CompactableChatHistoryTestHarness.CreateAsync(viaDeepAgent: true);

            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-1")], harness.Session);
            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-2")], harness.Session);

            List<ChatMessage> storedAfterTurn2 = harness.History.GetMessages(harness.Session);
            Assert.True(harness.Session.IsCompactionTriggered());
            Assert.True(harness.Client.Invocations[1].Messages.Count < storedAfterTurn2.Count);

            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-3")], harness.Session);

            List<ChatMessage> stored = harness.History.GetMessages(harness.Session);
            RecordingChatClientInvocation lastInvocation = harness.Client.Invocations[^1];

            Assert.True(stored.Count > lastInvocation.Messages.Count);
            Assert.True(harness.Session.IsCompactionTriggered());
        }

        [Fact]
        public async Task RunAsync_Should_ApplyIncrementalCompaction_When_SecondCompactionTurn()
        {
            CompactableChatHistoryTestHarness harness = await CompactableChatHistoryTestHarness.CreateAsync();

            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-1")], harness.Session);
            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-2")], harness.Session);
            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-3")], harness.Session);

            int countAfterFirstCompaction = harness.Client.Invocations[^1].Messages.Count;
            int storedAfter3 = harness.History.GetMessages(harness.Session).Count;
            Assert.True(harness.Session.IsCompactionTriggered());

            await harness.Agent.RunAsync([new ChatMessage(ChatRole.User, "turn-4")], harness.Session);

            RecordingChatClientInvocation secondCompactionInvocation = harness.Client.Invocations[^1];
            Assert.NotEmpty(secondCompactionInvocation.Messages);
            Assert.True(secondCompactionInvocation.Messages.Count <= countAfterFirstCompaction);
            Assert.True(secondCompactionInvocation.Messages.Count < storedAfter3 + 2);
            Assert.Equal(8, harness.History.GetMessages(harness.Session).Count);
            AssertContainsUserTexts(harness.History.GetMessages(harness.Session), "turn-1", "turn-2", "turn-3", "turn-4");
        }

        private static string GetText(ChatMessage message) =>
            message.Text ?? message.Contents.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;

        private static void AssertContainsUserTexts(IEnumerable<ChatMessage> messages, params string[] texts)
        {
            List<string> userTexts = messages
                .Where(m => m.Role == ChatRole.User)
                .Select(GetText)
                .ToList();

            foreach (string text in texts)
            {
                Assert.Contains(text, userTexts);
            }
        }

        private static async Task RunTurnsAsync(CompactableChatHistoryTestHarness harness, int count)
        {
            for (int i = 1; i <= count; i++)
            {
                await harness.Agent.RunAsync(
                    [new ChatMessage(ChatRole.User, $"turn-{i}")],
                    harness.Session);
            }
        }

        private static async Task RunStreamingTurnAsync(CompactableChatHistoryTestHarness harness, string userText)
        {
            List<AgentResponseUpdate> updates = [];
            await foreach (AgentResponseUpdate update in harness.Agent.RunStreamingAsync(
                               [new ChatMessage(ChatRole.User, userText)],
                               harness.Session))
            {
                updates.Add(update);
            }

            Assert.NotEmpty(updates);
        }
    }
}
