using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace DeepAgentNet.TodoLists.Internal
{
    internal class TodoListProvider : AIContextProvider
    {
        private readonly TodoListProviderOptions? _options;
        private readonly string _instruction;
        private readonly OnTodosUpdatedAsync? _onTodosUpdatedAsync;

        public TodoListProvider(TodoListProviderOptions? options = null)
        {
            _options = options;
            _instruction = options?.SystemPrompt ?? TodoListDefaults.SystemPrompt;
            _onTodosUpdatedAsync = options?.OnTodosUpdatedAsync;
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            return new(new AIContext
            {
                Instructions = _instruction,
                Tools = [CreateWriteTodosTool(context.Agent.Id, _options)]
            });
        }

        private AITool CreateWriteTodosTool(string agentId, TodoListProviderOptions? options)
        {
            return AIFunctionFactory.Create(WriteTodosAsync, new AIFunctionFactoryOptions
            {
                Name = TodoListDefaults.ToolName,
                Description = options?.ToolDescription ?? TodoListDefaults.ToolDescription,
            });

            async ValueTask<string> WriteTodosAsync([Description("List of todo items to update")] List<Todo> todos, AIFunctionArguments arguments, CancellationToken cancellation)
            {
                if (arguments.TryGetValue(TodoListDefaults.KeyDuplicate, out object? duplicate) && duplicate is true)
                {
                    return $"Error: The `{TodoListDefaults.ToolName}` tool should never be called multiple times " +
                        "in parallel. Please call it only once per model invocation to update the todo list.";
                }

                if (_onTodosUpdatedAsync is not null)
                {
                    await _onTodosUpdatedAsync(agentId, todos, cancellation);
                }

                return $"Updated todo list to {JsonSerializer.Serialize(todos, AIJsonUtilities.DefaultOptions)}";
            }
        }
    }
}
