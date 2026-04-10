using DeepAgentNet.Agents.Internal.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        public static FunctionInvokingChatClient AsFunctionInvokingChatClient(this IChatClient chatClient, IServiceProvider? services = null)
        {
            if (chatClient.GetService<FunctionInvokingChatClient>() is { } inner)
            {
                return inner;
            }

            var loggerFactory = services?.GetService<ILoggerFactory>();
            return new FunctionInvokingChatClient(chatClient, loggerFactory);
        }
    }
}
