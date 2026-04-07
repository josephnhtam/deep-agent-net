using DeepAgentNet.Agents.Internal;
using DeepAgentNet.Agents.Internal.Contracts;
using DeepAgentNet.Compactions.Internal;
using DeepAgentNet.FileSystems.Internal;
using DeepAgentNet.SubAgents;
using DeepAgentNet.SubAgents.Internal;
using DeepAgentNet.TodoLists.Internal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DeepAgentNet.Agents
{
    public static class DeepAgentExtensions
    {
        public static AIAgent AsDeepAgent(
            this IChatClient client,
            ChatClientAgentOptions agentOptions,
            DeepAgentOptions deepAgentOptions,
            ILoggerFactory? loggerFactory = null,
            IServiceProvider? services = null)
        {
            client = DecorateChatClient(client, deepAgentOptions);
            ChatClientAgentOptions defaultAgentOptions = agentOptions.Clone();

            ChatClientAgentOptions masterAgentOptions = CreateMasterAgentOptions(
                client, deepAgentOptions, loggerFactory, services, defaultAgentOptions);

            AIAgent agent = new ChatClientAgent(client, masterAgentOptions, loggerFactory, services);
            return agent.AsDeepAgent();
        }

        private static IChatClient DecorateChatClient(IChatClient client, DeepAgentOptions deepAgentOptions)
        {
            if (deepAgentOptions.Compaction is not null)
                client = client.AsCompactionChatClient(deepAgentOptions.Compaction);

            IFunctionCallPreValidValidator preValidator = CreateFunctionCallPreValidValidator(deepAgentOptions);
            client = client.AsFunctionCallPreValidatingChatClient(preValidator);

            client = client.AsTodoListChatClient(deepAgentOptions.TodoList);

            return client;
        }

        private static ChatClientAgentOptions CreateMasterAgentOptions(
            IChatClient client, DeepAgentOptions deepAgentOptions,
            ILoggerFactory? loggerFactory, IServiceProvider? services, ChatClientAgentOptions defaultAgentOptions)
        {
            ChatClientAgentOptions masterAgentOptions = defaultAgentOptions.Clone();

            masterAgentOptions.AIContextProviders =
            [
                ..masterAgentOptions.AIContextProviders ?? [],
                ..CreateDeepAgentContextProviders()
            ];

            List<AIContextProvider> CreateDeepAgentContextProviders() =>
                CollectContextProviders(
                    CreateTodoListProvider(deepAgentOptions),
                    CreateSubAgentProvider(client, defaultAgentOptions, deepAgentOptions, loggerFactory, services),
                    CreateFileSystemProvider(deepAgentOptions)
                );

            return masterAgentOptions;
        }

        private static TodoListProvider CreateTodoListProvider(DeepAgentOptions deepAgentOptions) =>
            new(deepAgentOptions.TodoList);

        private static FileSystemProvider? CreateFileSystemProvider(DeepAgentOptions deepAgentOptions) =>
            deepAgentOptions.FileSystem != null ? new(deepAgentOptions.FileSystem) : null;

        private static AIContextProvider CreateSubAgentProvider(IChatClient client, ChatClientAgentOptions options,
            DeepAgentOptions deepAgentOptions, ILoggerFactory? loggerFactory, IServiceProvider? services)
        {
            SubAgentDefaultOptions defaultOptions = new(
                DefaultChatClient: client,
                DefaultOptions: options,
                DefaultContextProviders: CreateDefaultContextProviders()
            );

            return new SubAgentProvider(defaultOptions, deepAgentOptions.SubAgent, loggerFactory, services);

            List<AIContextProvider> CreateDefaultContextProviders() => CollectContextProviders(
                CreateTodoListProvider(deepAgentOptions),
                CreateFileSystemProvider(deepAgentOptions)
            );
        }

        private static List<AIContextProvider> CollectContextProviders(params AIContextProvider?[] providers) =>
            providers.Where(provider => provider != null).ToList()!;

        private static IFunctionCallPreValidValidator CreateFunctionCallPreValidValidator(DeepAgentOptions deepAgentOptions)
        {
            FunctionCallPreValidValidator validator = new();

            if (deepAgentOptions.FileSystem is not null)
                new FileSystemPreValidator(deepAgentOptions.FileSystem.Access).Register(validator);

            return validator;
        }
    }
}
