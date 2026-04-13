using DeepAgentNet.Agents.Internal;
using DeepAgentNet.Agents.Internal.Contracts;
using DeepAgentNet.Compactions.Internal;
using DeepAgentNet.FileSystems.Internal;
using DeepAgentNet.Shells.Internal;
using DeepAgentNet.SubAgents;
using DeepAgentNet.SubAgents.Internal;
using DeepAgentNet.TodoLists.Internal;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
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
            client = BuildChatClient(client, deepAgentOptions, loggerFactory, services);
            ChatClientAgentOptions defaultAgentOptions = agentOptions.Clone();

            ChatClientAgentOptions masterAgentOptions = CreateMasterAgentOptions(
                client, deepAgentOptions, loggerFactory, services, defaultAgentOptions);

            AIAgent agent = new ChatClientAgent(client, masterAgentOptions, loggerFactory, services);
            return agent.AsDeepAgent();
        }

        private static IChatClient BuildChatClient(
            IChatClient client, DeepAgentOptions deepAgentOptions, ILoggerFactory? loggerFactory, IServiceProvider? services)
        {
            ChatClientBuilder builder = client.AsBuilder();

            builder = builder.Use(inner => inner.AsFunctionInvokingChatClient(deepAgentOptions.FunctionInvocation, loggerFactory, services));

            IFunctionCallPreValidValidator preValidator = CreateFunctionCallPreValidValidator(deepAgentOptions);
            builder = builder.Use(inner => inner.AsFunctionCallPreValidatingChatClient(preValidator));

            if (deepAgentOptions.TodoList is not null)
                builder = builder.Use(inner => inner.AsTodoListChatClient(deepAgentOptions.TodoList));

            if (deepAgentOptions.Compaction is not null)
                builder.UseCompactionProvider(deepAgentOptions.Compaction);

            builder = builder.Use(inner => inner.AsCallIdSetterChatClient());

            return builder.Build();
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
                    CreateFileSystemProvider(deepAgentOptions),
                    CreateShellProvider(deepAgentOptions)
                );

            return masterAgentOptions;
        }

        private static TodoListProvider? CreateTodoListProvider(DeepAgentOptions deepAgentOptions) =>
            deepAgentOptions.TodoList is not null ? new(deepAgentOptions.TodoList) : null;

        private static FileSystemProvider? CreateFileSystemProvider(DeepAgentOptions deepAgentOptions) =>
            deepAgentOptions.FileSystem != null ? new(deepAgentOptions.FileSystem) : null;

        private static ShellProvider? CreateShellProvider(DeepAgentOptions deepAgentOptions) =>
            deepAgentOptions.Shell != null ? new(deepAgentOptions.Shell) : null;

        private static AIContextProvider CreateSubAgentProvider(IChatClient client, ChatClientAgentOptions options,
            DeepAgentOptions deepAgentOptions, ILoggerFactory? loggerFactory, IServiceProvider? services)
        {
            SubAgentDefaultOptions defaultOptions = new(
                DefaultChatClient: client,
                DefaultOptions: options,
                DefaultContextProviders: CreateDefaultContextProviders(),
                DeepAgentOptions: deepAgentOptions
            );

            return new SubAgentProvider(defaultOptions, deepAgentOptions.SubAgent, loggerFactory, services);

            List<AIContextProvider> CreateDefaultContextProviders() => CollectContextProviders(
                CreateTodoListProvider(deepAgentOptions),
                CreateFileSystemProvider(deepAgentOptions),
                CreateShellProvider(deepAgentOptions)
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
