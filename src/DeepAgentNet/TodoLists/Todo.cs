using System.ComponentModel;

namespace DeepAgentNet.TodoLists
{
    public record Todo(
        [Description("Content of the todo item")]
        string Content,
        TodoStatus Status);
}
