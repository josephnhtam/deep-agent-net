namespace DeepAgentNet.TodoLists.Internal
{
    internal record TodoListState(int CurrentTurns, IReadOnlyList<Todo> Todos)
    {
        public const string StateBagKey = "TodoListState";
    }
}
