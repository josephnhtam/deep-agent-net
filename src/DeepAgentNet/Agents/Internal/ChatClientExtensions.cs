using DeepAgentNet.Agents.Internal.Contracts;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Agents.Internal
{
    internal static class ChatClientExtensions
    {
        public static FunctionCallPreValidatingChatClient AsFunctionCallPreValidatingChatClient(this IChatClient chatClient, IFunctionCallPreValidValidator validator)
        {
            if (chatClient.GetService<FunctionCallPreValidatingChatClient>() is { } inner)
            {
                return inner;
            }

            return new FunctionCallPreValidatingChatClient(chatClient, validator);
        }
    }
}
