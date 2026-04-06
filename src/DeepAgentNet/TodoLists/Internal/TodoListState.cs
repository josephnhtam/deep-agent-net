namespace DeepAgentNet.TodoLists.Internal
{
    public record TodoListState(int CurrentTurns, IReadOnlyList<Todo> Todos)
    {
        public const string StateBagKey = "TodoListState";
    }
}
