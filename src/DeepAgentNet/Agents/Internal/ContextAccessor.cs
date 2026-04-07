using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Agents.Internal
{
    public static class ContextAccessor
    {
        public static ChatOptions? Options =>
            FunctionInvokingChatClient.CurrentContext?.Options ??
            FunctionCallPreValidValidatingChatClient.CurrentContext?.Options;

        public static AgentSession? Session => Options?.GetSession();

        public static AIAgent? Agent => Options?.GetAgent();
    }
}
