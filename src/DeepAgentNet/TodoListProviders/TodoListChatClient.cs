using Microsoft.Extensions.AI;

namespace DeepAgentNet.TodoListProviders
{
    internal class TodoListChatClient : DelegatingChatClient
    {
        private readonly TodoListProviderOptions? _options;

        public TodoListChatClient(IChatClient innerClient, TodoListProviderOptions? options) : base(innerClient)
        {
            _options = options;
        }

        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = new CancellationToken())
        {
            ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken);

            List<FunctionCallContent> todosWrites = response.Messages.SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .Where(c => c.Name == TodoListProviderDefaults.ToolName)
                .ToList();

            if (todosWrites.Count <= 1)
                return response;

            foreach (FunctionCallContent todosWrite in todosWrites)
            {
                todosWrite.Arguments ??= new Dictionary<string, object?>();
                todosWrite.Arguments[TodoListProviderDefaults.KeyDuplicate] = true;
            }

            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = new CancellationToken())
        {
            List<ChatResponseUpdate> todosWriteUpdates = new();

            await foreach (ChatResponseUpdate update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                if (update.Contents.OfType<FunctionCallContent>().Any(c => c.Name == TodoListProviderDefaults.ToolName) != true)
                {
                    todosWriteUpdates.Add(update);
                    continue;
                }

                yield return update;
            }

            if (todosWriteUpdates.Count > 1)
            {
                var todosWrites = todosWriteUpdates.SelectMany(u => u.Contents).OfType<FunctionCallContent>()
                    .Where(c => c.Name == TodoListProviderDefaults.ToolName);

                foreach (FunctionCallContent todosWrite in todosWrites)
                {
                    todosWrite.Arguments ??= new Dictionary<string, object?>();
                    todosWrite.Arguments[TodoListProviderDefaults.KeyDuplicate] = true;
                }
            }

            foreach (ChatResponseUpdate update in todosWriteUpdates)
            {
                yield return update;
            }
        }
    }
}
