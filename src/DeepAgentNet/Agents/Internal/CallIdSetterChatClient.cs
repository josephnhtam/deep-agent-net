using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace DeepAgentNet.Agents.Internal
{
    internal class CallIdSetterChatClient : DelegatingChatClient
    {
        public CallIdSetterChatClient(IChatClient innerClient) : base(innerClient)
        {
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);

            foreach (var message in response.Messages)
            {
                UpdateCallContentsWithCallIdIfNotExist(message.Contents);
            }

            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = base.GetStreamingResponseAsync(messages, options, cancellationToken);

            await foreach (var update in response)
            {
                UpdateCallContentsWithCallIdIfNotExist(update.Contents);
                yield return update;
            }
        }

        private void UpdateCallContentsWithCallIdIfNotExist(IList<AIContent> contents)
        {
            for (var i = 0; i < contents.Count; i++)
            {
                if (contents[i] is not FunctionCallContent fnCallContent ||
                    !string.IsNullOrWhiteSpace(fnCallContent.CallId))
                {
                    continue;
                }

                var callId = Guid.NewGuid().ToString();

                contents[i] = new FunctionCallContent(
                    callId, fnCallContent.Name, fnCallContent.Arguments);
            }
        }
    }
}
