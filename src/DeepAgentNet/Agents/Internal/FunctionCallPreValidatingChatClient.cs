using DeepAgentNet.Agents.Internal.Contracts;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace DeepAgentNet.Agents.Internal
{
    internal class FunctionCallPreValidatingChatClient : DelegatingChatClient
    {
        public IFunctionCallPreValidValidator FunctionCallPreValidator { get; }

        private const string KeyPreValidationRejected = "PreValidationRejected";

        internal FunctionCallPreValidatingChatClient(IChatClient innerClient, IFunctionCallPreValidValidator? functionCallPreValidValidator = null) : base(innerClient)
        {
            FunctionCallPreValidator = functionCallPreValidValidator ?? new FunctionCallPreValidValidator();
        }

        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            List<ChatMessage> originalMessages = [..messages];
            IList<ChatMessage> chatMessages = originalMessages;

            List<ChatMessage> responseMessages = [];
            List<ChatMessage>? augmentedMessages = null;
            List<ChatMessage>? addedMessages = null;
            bool lastIterationHadConversationId = false;
            string? conversationId = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool preValidationRejected = false;
                addedMessages?.Clear();

                ChatResponse response = await base.GetResponseAsync(chatMessages, options, cancellationToken);
                conversationId = response.ConversationId ?? conversationId;

                foreach (var message in response.Messages)
                {
                    ChatMessage? addedMessage =
                        await ProcessFunctionCallPreValidationAsync(message.Contents, cancellationToken);

                    if (addedMessage is not null)
                    {
                        preValidationRejected = true;
                        (addedMessages ??= []).Add(addedMessage);
                        response.Messages.Add(addedMessage);
                    }
                }

                FixupHistories(
                    originalMessages,
                    ref chatMessages,
                    ref augmentedMessages,
                    response,
                    responseMessages,
                    addedMessages,
                    ref lastIterationHadConversationId
                );

                responseMessages.AddRange(response.Messages);

                if (!preValidationRejected ||
                    HasUnprocessedFunctionCallRequest(response) ||
                    HasToolApprovalRequest(response))
                {
                    return new ChatResponse(responseMessages)
                    {
                        ConversationId = conversationId
                    };
                }

                UpdateOptionsForNextIteration(ref options, response.ConversationId);
            }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            List<ChatMessage> originalMessages = [..messages];
            IList<ChatMessage> chatMessages = originalMessages;

            List<ChatMessage> responseMessages = [];
            List<ChatMessage>? augmentedMessages = null;
            List<ChatMessage>? addedMessages = null;
            bool lastIterationHadConversationId = false;

            List<ChatResponseUpdate> updates = [];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool preValidationRejected = false;
                addedMessages?.Clear();

                await foreach (ChatResponseUpdate update in
                    base.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
                {
                    ChatMessage? addedMessage = await ProcessFunctionCallPreValidationAsync(update.Contents, cancellationToken);

                    updates.Add(update);
                    yield return update;

                    if (addedMessage is not null)
                    {
                        var addedUpdate = ConvertToolResultMessageToUpdate(
                            addedMessage, options?.ConversationId, addedMessage.MessageId);

                        preValidationRejected = true;
                        (addedMessages ??= []).Add(addedMessage);

                        updates.Add(addedUpdate);
                        yield return addedUpdate;
                    }
                }

                ChatResponse response = updates.ToChatResponse();

                FixupHistories(
                    originalMessages,
                    ref chatMessages,
                    ref augmentedMessages,
                    response,
                    responseMessages,
                    addedMessages,
                    ref lastIterationHadConversationId
                );

                responseMessages.AddRange(response.Messages);

                if (!preValidationRejected ||
                    HasUnprocessedFunctionCallRequest(response) ||
                    HasToolApprovalRequest(response))
                {
                    break;
                }

                UpdateOptionsForNextIteration(ref options, response.ConversationId);
                updates.Clear();
            }
        }

        private async ValueTask<ChatMessage?> ProcessFunctionCallPreValidationAsync(
            IList<AIContent> contents, CancellationToken cancellationToken)
        {
            List<AIContent>? rejections = null;

            for (var i = 0; i < contents.Count; i++)
            {
                FunctionCallContent? call = GetFunctionCallContent(contents[i]);

                if (call is null)
                    continue;

                string? rejection = await FunctionCallPreValidator.PreValidateAsync(call, cancellationToken);

                if (rejection is not null)
                {
                    call.InformationalOnly = true;
                    contents[i] = call;
                    (rejections ??= []).Add(new FunctionResultContent(call.CallId, rejection)
                    {
                        AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            [KeyPreValidationRejected] = true
                        }
                    });
                }
            }

            if (rejections is not null)
            {
                return new ChatMessage(ChatRole.Tool, rejections)
                {
                    MessageId = $"PreValidation:{Guid.NewGuid():N}"
                };
            }

            return null;
        }

        private static FunctionCallContent? GetFunctionCallContent(AIContent content) => content switch
        {
            FunctionCallContent { InformationalOnly: false } call => call,
            ToolApprovalRequestContent { ToolCall: FunctionCallContent call } => call,
            _ => null
        };

        private static bool HasUnprocessedFunctionCallRequest(ChatResponse response) =>
            response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
                .Any(c => !c.InformationalOnly);

        private static bool HasToolApprovalRequest(ChatResponse response) =>
            response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>()
                .Any();

        private static ChatResponseUpdate ConvertToolResultMessageToUpdate(
            ChatMessage message, string? conversationId, string? messageId) => new()
        {
            AdditionalProperties = message.AdditionalProperties,
            AuthorName = message.AuthorName,
            ConversationId = conversationId,
            CreatedAt = DateTimeOffset.UtcNow,
            Contents = message.Contents,
            RawRepresentation = message.RawRepresentation,
            ResponseId = messageId,
            MessageId = messageId,
            Role = message.Role,
        };

        private static void UpdateOptionsForNextIteration(ref ChatOptions? options, string? conversationId)
        {
            if (conversationId is null)
                return;

            if (options is null)
            {
                options = new ChatOptions { ConversationId = conversationId };
            }
            else if (options.ConversationId != conversationId)
            {
                options = options.Clone();
                options.ConversationId = conversationId;
            }
        }

        private static void FixupHistories(
            IEnumerable<ChatMessage> originalMessages,
            ref IList<ChatMessage> messages,
            ref List<ChatMessage>? augmentedHistory,
            ChatResponse response,
            List<ChatMessage> allTurnsResponseMessages,
            List<ChatMessage>? addedMessages,
            ref bool lastIterationHadConversationId)
        {
            if (response.ConversationId is not null)
            {
                if (augmentedHistory is not null)
                {
                    augmentedHistory.Clear();
                }
                else
                {
                    augmentedHistory = [];
                }

                if (addedMessages is not null)
                {
                    augmentedHistory.AddRange(addedMessages);
                }

                lastIterationHadConversationId = true;
            }
            else if (lastIterationHadConversationId)
            {
                augmentedHistory ??= [];
                augmentedHistory.Clear();
                augmentedHistory.AddRange(originalMessages);
                augmentedHistory.AddRange(allTurnsResponseMessages);

                if (addedMessages is not null)
                {
                    augmentedHistory.AddRange(addedMessages);
                }

                lastIterationHadConversationId = false;
            }
            else
            {
                augmentedHistory ??= originalMessages.ToList();
                augmentedHistory.AddMessages(response);
                lastIterationHadConversationId = false;
            }

            messages = augmentedHistory;
        }
    }
}
