using DeepAgentNet.TodoLists.Internal.Tools;
using Microsoft.Agents.AI;

namespace DeepAgentNet.TodoLists.Internal
{
    internal class TodoListProvider : AIContextProvider
    {
        private readonly TodoListProviderOptions _options;

        public TodoListProvider(TodoListProviderOptions? options = null)
        {
            _options = options ?? new TodoListProviderOptions();
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            WriteTodosToolProvider toolProvider = new(context.Agent.Id, _options.OnTodosUpdatedAsync, _options.ToolDescription);

            return new(new AIContext
            {
                Instructions = _options.SystemPrompt ?? TodoListDefaults.SystemPrompt,
                Tools = [toolProvider.Tool]
            });
        }
    }
}
