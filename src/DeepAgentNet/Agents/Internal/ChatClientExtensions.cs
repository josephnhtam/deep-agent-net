using DeepAgentNet.Agents.Internal.Contracts;
using Microsoft.Extensions.AI;
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

        public static FunctionInvokingChatClient AsFunctionInvokingChatClient(this IChatClient chatClient, DeepAgentOptions options, ILoggerFactory? loggerFactory = null, IServiceProvider? serviceProvider = null)
        {
            if (chatClient.GetService<FunctionInvokingChatClient>() is { } inner)
            {
                return inner;
            }

            var client = new FunctionInvokingChatClient(chatClient, loggerFactory, serviceProvider)
            {
                MaximumIterationsPerRequest = options.FunctionInvocation.MaximumIterationsPerRequest,
                AllowConcurrentInvocation = options.FunctionInvocation.AllowConcurrentInvocation,
                MaximumConsecutiveErrorsPerRequest = options.FunctionInvocation.MaximumConsecutiveErrorsPerRequest
            };

            return client;
        }

        public static CallIdSetterChatClient AsCallIdSetterChatClient(this IChatClient chatClient)
        {
            if (chatClient.GetService<CallIdSetterChatClient>() is { } inner)
            {
                return inner;
            }

            return new CallIdSetterChatClient(chatClient);
        }
    }
}
