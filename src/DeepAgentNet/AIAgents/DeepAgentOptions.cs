using DeepAgentNet.SubAgents;
using DeepAgentNet.TodoLists;

namespace DeepAgentNet.AIAgents
{
    public record DeepAgentOptions
    {
        public TodoListProviderOptions? TodoList { get; init; }
        public SubAgentProviderOptions? SubAgent { get; init; }
    }
}
