namespace DeepAgentNet.TodoListProviders
{
    public record TodoListProviderOptions
    {
        public string? SystemPrompt { get; init; }
        public string? ToolDescription { get; init; }
        public Func<List<Todo>, CancellationToken, ValueTask>? OnTodosUpdatedAsync { get; init; }
    }
}
