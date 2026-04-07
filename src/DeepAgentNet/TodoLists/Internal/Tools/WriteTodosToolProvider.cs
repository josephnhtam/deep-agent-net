using DeepAgentNet.Agents;
using DeepAgentNet.Agents.Internal;
using DeepAgentNet.Shared.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace DeepAgentNet.TodoLists.Internal.Tools
{
    internal class WriteTodosToolProvider : IToolProvider
    {
        private readonly string _agentId;
        private readonly OnTodosUpdatedAsync? _onTodosUpdatedAsync;

        public AITool Tool { get; }

        public WriteTodosToolProvider(string agentId, OnTodosUpdatedAsync? onTodosUpdatedAsync, string? description = null)
        {
            _agentId = agentId;
            _onTodosUpdatedAsync = onTodosUpdatedAsync;

            Tool = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = TodoListDefaults.ToolName,
                Description = description ?? TodoListDefaults.ToolDescription,
            });
        }

        private async ValueTask<string> ExecuteAsync(
            [Description("The complete updated todo list, including all items and their current statuses")]
            List<Todo> todos,
            AIFunctionArguments arguments,
            CancellationToken cancellation)
        {
            if (_onTodosUpdatedAsync is not null)
            {
                await _onTodosUpdatedAsync(_agentId, todos, cancellation).ConfigureAwait(false);
            }

            UpdateSessionTodoList(todos);

            return $"""
                Todo list has been updated successfully to 
                ```json 
                {JsonSerializer.Serialize(todos)}
                ```

                Ensure that you continue to use the todo list to track your progress.
                Please proceed with the current tasks if applicable."
                """;
        }

        private static void UpdateSessionTodoList(List<Todo> todos)
        {
            AgentSession? session = ContextAccessor.Session;

            if (session is null)
                return;

            session.StateBag.SetValue(TodoListState.StateBagKey, new TodoListState(
                CurrentTurns: 0,
                Todos: todos
            ));
        }
    }
}
