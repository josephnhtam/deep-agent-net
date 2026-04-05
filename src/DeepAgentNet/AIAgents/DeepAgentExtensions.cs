using DeepAgentNet.Compactions.Internal;
using DeepAgentNet.FileSystems.Internal;
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
            client = client.AsTodoListChatClient(deepAgentOptions?.TodoList);

            if (deepAgentOptions?.Compaction is not null)
                client = client.AsCompactionChatClient(deepAgentOptions.Compaction);

            List<AIContextProvider> deepAgentContextProviders = CreateDeepAgentContextProviders(options.Clone());

            options = options.Clone();
            options.AIContextProviders = [..options.AIContextProviders ?? [], ..deepAgentContextProviders];

            AIAgent agent = new ChatClientAgent(client, options, loggerFactory, services);
            return new DeepAgent(agent);

            List<AIContextProvider> CreateDeepAgentContextProviders(ChatClientAgentOptions agentOptions) =>
                CollectContextProviders(
                    CreateTodoListProvider(deepAgentOptions),
                    CreateSubAgentProvider(client, agentOptions, deepAgentOptions, loggerFactory, services),
                    CreateFileSystemProvider(deepAgentOptions)
                );
        }

        private static TodoListProvider CreateTodoListProvider(DeepAgentOptions? deepAgentOptions) =>
            new(deepAgentOptions?.TodoList);

        private static FileSystemProvider? CreateFileSystemProvider(DeepAgentOptions? deepAgentOptions) =>
            deepAgentOptions?.FileSystem != null ? new(deepAgentOptions.FileSystem) : null;

        private static AIContextProvider CreateSubAgentProvider(IChatClient client, ChatClientAgentOptions options,
            DeepAgentOptions? deepAgentOptions, ILoggerFactory? loggerFactory, IServiceProvider? services)
        {
            SubAgentDefaultOptions defaultOptions = new(
                DefaultChatClient: client,
                DefaultOptions: options,
                DefaultContextProviders: CreateDefaultContextProviders(),
                DefaultGeneralPurposeContextProviders: CreateGeneralPurposeContextProviders()
            );

            return new SubAgentProvider(defaultOptions, deepAgentOptions?.SubAgent, loggerFactory, services);

            List<AIContextProvider> CreateDefaultContextProviders() => CollectContextProviders(
                CreateTodoListProvider(deepAgentOptions),
                CreateFileSystemProvider(deepAgentOptions)
            );

            List<AIContextProvider> CreateGeneralPurposeContextProviders() => CollectContextProviders(
                CreateTodoListProvider(deepAgentOptions),
                CreateFileSystemProvider(deepAgentOptions)
            );
        }

        private static List<AIContextProvider> CollectContextProviders(params AIContextProvider?[] providers) =>
            providers.Where(provider => provider != null).ToList()!;
    }
}
