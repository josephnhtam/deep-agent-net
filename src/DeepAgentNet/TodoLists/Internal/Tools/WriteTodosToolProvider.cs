using DeepAgentNet.Shared.Contracts;
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
            [Description("List of todo items to update")]
            List<Todo> todos,
            AIFunctionArguments arguments,
            CancellationToken cancellation)
        {
            if (arguments.TryGetValue(TodoListDefaults.KeyDuplicate, out object? duplicate) && duplicate is true)
            {
                return $"Error: The `{TodoListDefaults.ToolName}` tool should never be called multiple times " +
                    "in parallel. Please call it only once per model invocation to update the todo list.";
            }

            if (_onTodosUpdatedAsync is not null)
            {
                await _onTodosUpdatedAsync(_agentId, todos, cancellation).ConfigureAwait(false);
            }

            return $"Updated todo list to {JsonSerializer.Serialize(todos, AIJsonUtilities.DefaultOptions)}";
        }
    }
}
