using Microsoft.Extensions.AI;

namespace DeepAgentNet.TodoListProviders
{
    internal static class TodoListChatClientExtensions
    {
        public static TodoListChatClient AsTodoListChatClient(this IChatClient chatClient, TodoListProviderOptions? options = null)
        {
            if (chatClient.GetService<TodoListChatClient>() is { } inner)
            {
                return inner;
            }

            return new TodoListChatClient(chatClient, options);
        }
    }
}
