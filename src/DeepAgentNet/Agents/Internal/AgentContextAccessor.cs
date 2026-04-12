using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Agents.Internal
{
    internal static class AgentContextAccessor
    {
        public static ChatOptions? Options =>
            FunctionInvokingChatClient.CurrentContext?.Options ??
            FunctionCallPreValidatingChatClient.CurrentContext?.Options;

        public static AgentSession? Session => Options?.GetSession();

        public static AIAgent? Agent => Options?.GetAgent();
    }
}
