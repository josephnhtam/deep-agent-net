using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DeepAgentNet.TodoLists.Internal
{
    internal class TodoListChatClient : DelegatingChatClient
    {
        private readonly int _reminderTurnThreshold;
        public TodoListProviderOptions ProviderOptions { get; }

        internal TodoListChatClient(IChatClient innerClient, TodoListProviderOptions? options) : base(innerClient)
        {
            ProviderOptions = options ?? new TodoListProviderOptions();
            _reminderTurnThreshold = ProviderOptions.ReminderTurnThreshold ?? TodoListDefaults.DefaultReminderTurnThreshold;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            messages = TryInjectTodoReminder(messages, options);

            ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);

            IncrementTurnCounter(options);
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            messages = TryInjectTodoReminder(messages, options);

            await foreach (ChatResponseUpdate update in
                base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }

            IncrementTurnCounter(options);
        }

        private IEnumerable<ChatMessage> TryInjectTodoReminder(
            IEnumerable<ChatMessage> messages, ChatOptions? options)
        {
            if (_reminderTurnThreshold <= 0)
                return messages;

            TodoListState? state = GetTodoListState(options);

            if (state is null || state.CurrentTurns < _reminderTurnThreshold || state.Todos.Count == 0)
                return messages;

            bool hasActiveTodos = state.Todos.Any(t =>
                t.Status is TodoStatus.Pending or TodoStatus.InProgress);

            if (!hasActiveTodos)
                return messages;

            List<ChatMessage> messageList = messages as List<ChatMessage> ?? messages.ToList();
            messageList.Add(new ChatMessage(ChatRole.User, BuildReminderMessage(state.Todos))
            {
                MessageId = $"todo:{Guid.NewGuid():N}"
            });
            return messageList;
        }

        private static string BuildReminderMessage(IReadOnlyList<Todo> todos)
        {
            return $"""
                The `{TodoListDefaults.ToolName}` tool hasn't been used recently.

                Here are the existing contents of your todo list:
                ```json 
                {JsonSerializer.Serialize(todos)}
                ```

                Consider using the `{TodoListDefaults.ToolName}` tool to update your progress.
                Make sure to preserve the status of previously completed items.
                Only use it if it's relevant to the current work.
                This is just a gentle reminder, ignore if not applicable.
                """;
        }

        private static void IncrementTurnCounter(ChatOptions? options)
        {
            AgentSession? session = AIAgent.CurrentRunContext?.Session;
            if (session is null)
                return;

            TodoListState? state = session.StateBag.GetValue<TodoListState>(TodoListState.StateBagKey);
            if (state is null)
                return;

            session.StateBag.SetValue(TodoListState.StateBagKey, state with
            {
                CurrentTurns = state.CurrentTurns + 1
            });
        }

        private static TodoListState? GetTodoListState(ChatOptions? options)
        {
            AgentSession? session = AIAgent.CurrentRunContext?.Session;
            return session?.StateBag.GetValue<TodoListState>(TodoListState.StateBagKey);
        }
    }
}
