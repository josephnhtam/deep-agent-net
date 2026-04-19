using DeepAgentNet.ChatHistories.Internal;
using Microsoft.Agents.AI;

namespace DeepAgentNet.Compactions
{
    public static class CompactionSessionExtensions
    {
        public static bool IsCompactionTriggered(this AgentSession session)
        {
            return CompactableChatHistoryProvider.TriggeredSessionState
                .GetOrInitializeState(session).Triggered;
        }
    }
}
