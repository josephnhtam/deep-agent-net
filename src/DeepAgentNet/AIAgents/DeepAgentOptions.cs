using DeepAgentNet.TodoListProviders;

namespace DeepAgentNet.AIAgents
{
    public record DeepAgentOptions
    {
        public TodoListProviderOptions? TodoList { get; init; }
    }
}
