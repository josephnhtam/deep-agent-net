using DeepAgentNet.Agents.Internal.Contracts;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Agents.Internal
{
    internal static class FunctionCallPreValidValidatingChatClientExtensions
    {
        public static FunctionCallPreValidValidatingChatClient AsFunctionCallPreValidValidatingChatClient(this IChatClient chatClient, IFunctionCallPreValidValidator validator)
        {
            if (chatClient.GetService<FunctionCallPreValidValidatingChatClient>() is { } inner)
            {
                return inner;
            }

            return new FunctionCallPreValidValidatingChatClient(chatClient, validator);
        }
    }
}
