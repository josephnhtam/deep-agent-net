using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;

namespace DeepAgentNet.TodoListProviders
{
    internal class TodoListProvider : AIContextProvider
    {
        private readonly string _instruction;
        private readonly Func<List<Todo>, CancellationToken, ValueTask>? _onTodosUpdatedAsync;
        private readonly List<AITool> _tools;

        public TodoListProvider(TodoListProviderOptions? options = null)
        {
            _instruction = options?.SystemPrompt ?? TodoListProviderDefaults.SystemPrompt;
            _onTodosUpdatedAsync = options?.OnTodosUpdatedAsync;

            _tools =
            [
                AIFunctionFactory.Create(WriteTodos, new AIFunctionFactoryOptions
                {
                    Name = "write_todos",
                    Description = options?.ToolDescription ?? TodoListProviderDefaults.ToolDescription,
                })
            ];
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            return new(new AIContext
            {
                Instructions = _instruction,
                Tools = _tools
            });
        }

        private async ValueTask<string> WriteTodos([Description("List of todo items to update")] List<Todo> todos, AIFunctionArguments arguments, CancellationToken cancellation)
        {
            if (arguments.TryGetValue(TodoListProviderDefaults.KeyDuplicate, out object? duplicate) && duplicate is true)
            {
                return $"Error: The `{TodoListProviderDefaults.ToolName}` tool should never be called multiple times " +
                    "in parallel. Please call it only once per model invocation to update the todo list.";
            }

            if (_onTodosUpdatedAsync is not null)
            {
                await _onTodosUpdatedAsync(todos, cancellation);
            }

            return $"Updated todo list to {JsonSerializer.Serialize(todos, AIJsonUtilities.DefaultOptions)}";
        }
    }
}
