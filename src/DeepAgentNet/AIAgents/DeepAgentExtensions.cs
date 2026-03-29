using DeepAgentNet.TodoListProviders;
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
            Func<IChatClient, IChatClient>? clientFactory = null,
            ILoggerFactory? loggerFactory = null,
            IServiceProvider? services = null)
        {
            options = options.Clone();

            List<AIContextProvider> aiContextProvider = options.AIContextProviders?.ToList() ?? [];
            aiContextProvider.Add(new TodoListProvider(deepAgentOptions?.TodoList));
            options.AIContextProviders = aiContextProvider;

            if (clientFactory is not null)
            {
                client = clientFactory(client);
            }

            client = client.AsTodoListChatClient(deepAgentOptions?.TodoList);

            ChatClientAgent agent = new ChatClientAgent(client, options, loggerFactory, services);
            return new DeepAgent(agent);
        }
    }
}
