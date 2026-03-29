using System.ComponentModel;

namespace DeepAgentNet.TodoListProviders
{
    public record Todo(
        [Description("Content of the todo item")]
        string Content,
        TodoStatus Status);
}
