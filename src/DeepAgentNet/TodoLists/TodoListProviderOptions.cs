namespace DeepAgentNet.TodoLists
{
    public delegate ValueTask OnTodosUpdatedAsync(string agentId, List<Todo> todos, CancellationToken cancellationToken);

    public record TodoListProviderOptions
    {
        public string? SystemPrompt { get; init; }
        public string? ToolDescription { get; init; }
        public OnTodosUpdatedAsync? OnTodosUpdatedAsync { get; init; }
    }
}
