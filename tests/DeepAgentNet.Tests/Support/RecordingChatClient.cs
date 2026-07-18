using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace DeepAgentNet.Tests.Support
{
    internal sealed class RecordingChatClient : IChatClient
    {
        public ChatClientMetadata Metadata { get; } = new("recording");

        public string ResponseText { get; set; } = "ok";

        public List<RecordingChatClientInvocation> Invocations { get; } = [];

        public void Dispose() { }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(new RecordingChatClientInvocation([.. chatMessages], options));
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, ResponseText)]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Invocations.Add(new RecordingChatClientInvocation([.. chatMessages], options));
            yield return new ChatResponseUpdate(ChatRole.Assistant, ResponseText);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }

    internal sealed record RecordingChatClientInvocation(
        IReadOnlyList<ChatMessage> Messages,
        ChatOptions? Options);
}
