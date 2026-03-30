using DeepAgentNet.SubAgents;
using DeepAgentNet.SubAgents.Internal;
using DeepAgentNet.TodoLists.Internal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DeepAgentNet.AIAgents
{
    public static class DeepAgentExtensions
    {
        public static DeepAgent AsDeepAgent(
            this IChatClient client,
            ChatClientAgentOptions options,
            DeepAgentOptions? deepAgentOptions = null,
            ILoggerFactory? loggerFactory = null,
            IServiceProvider? services = null)
        {
            var deepAgentContextProviders = CreateDeepAgentContextProviders(options.Clone());

            options = options.Clone();
            options.AIContextProviders = [..options.AIContextProviders ?? [], ..deepAgentContextProviders];
            client = client.AsTodoListChatClient(deepAgentOptions?.TodoList);

            ChatClientAgent agent = new ChatClientAgent(client, options, loggerFactory, services);
            return new DeepAgent(agent);

            List<AIContextProvider> CreateDeepAgentContextProviders(ChatClientAgentOptions agentOptions) =>
            [
                CreateTodoListProvider(deepAgentOptions),
                CreateSubAgentProvider(client, agentOptions, deepAgentOptions, loggerFactory, services)
            ];
        }

        private static TodoListProvider CreateTodoListProvider(DeepAgentOptions? deepAgentOptions) =>
            new(deepAgentOptions?.TodoList);

        private static AIContextProvider CreateSubAgentProvider(IChatClient client, ChatClientAgentOptions options,
            DeepAgentOptions? deepAgentOptions, ILoggerFactory? loggerFactory, IServiceProvider? services)
        {
            client = client.AsTodoListChatClient(deepAgentOptions?.TodoList);

            SubAgentDefaultOptions defaultOptions = new(
                DefaultChatClient: client,
                DefaultOptions: options,
                DefaultContextProviders: CreateDefaultContextProviders(),
                DefaultGeneralPurposeContextProviders: CreateGeneralPurposeContextProviders()
            );

            return new SubAgentProvider(defaultOptions, deepAgentOptions?.SubAgent, loggerFactory, services);

            List<AIContextProvider> CreateDefaultContextProviders() =>
            [
                CreateTodoListProvider(deepAgentOptions),
            ];

            List<AIContextProvider> CreateGeneralPurposeContextProviders() =>
            [
                CreateTodoListProvider(deepAgentOptions),
            ];
        }
    }
}
