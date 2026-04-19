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
            List<ChatMessage> originalMessages = [.. messages];
            IList<ChatMessage> chatMessages = originalMessages;

            List<ChatMessage> responseMessages = [];
            List<ChatMessage>? augmentedMessages = null;
            List<ChatMessage>? addedMessages = null;
            bool lastIterationHadConversationId = false;
            string? conversationId = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                addedMessages?.Clear();

                ChatResponse response = await base.GetResponseAsync(chatMessages, options, cancellationToken);
                conversationId = response.ConversationId ?? conversationId;

                List<PendingRejection>? pendingRejections = null;

                foreach (var message in response.Messages)
                {
                    pendingRejections = await CollectRejectionsAsync(message.Contents, pendingRejections, cancellationToken);
                }

                if (pendingRejections is not { Count: > 0 })
                {
                    FixupHistories(
                        originalMessages, ref chatMessages, ref augmentedMessages,
                        response, responseMessages, addedMessages, ref lastIterationHadConversationId);
                    responseMessages.AddRange(response.Messages);
                    return new ChatResponse(responseMessages) { ConversationId = conversationId };
                }

                var rejectedCallIds = new HashSet<string>(pendingRejections.Select(r => r.Call.CallId));

                if (HasNonRejectedFunctionCallOrApproval(response, rejectedCallIds))
                {
                    FixupHistories(
                        originalMessages, ref chatMessages, ref augmentedMessages,
                        response, responseMessages, addedMessages, ref lastIterationHadConversationId);
                    responseMessages.AddRange(response.Messages);
                    return new ChatResponse(responseMessages) { ConversationId = conversationId };
                }

                var rejections = ApplyRejections(pendingRejections);
                var addedMessage = CreateRejectionMessage(rejections);
                (addedMessages ??= []).Add(addedMessage);
                response.Messages.Add(addedMessage);

                FixupHistories(
                    originalMessages, ref chatMessages, ref augmentedMessages,
                    response, responseMessages, addedMessages, ref lastIterationHadConversationId);
                responseMessages.AddRange(response.Messages);

                UpdateOptionsForNextIteration(ref options, response.ConversationId);
            }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            List<ChatMessage> originalMessages = [.. messages];
            IList<ChatMessage> chatMessages = originalMessages;

            List<ChatMessage> responseMessages = [];
            List<ChatMessage>? augmentedMessages = null;
            List<ChatMessage>? addedMessages = null;
            bool lastIterationHadConversationId = false;

            List<ChatResponseUpdate> updates = [];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                addedMessages?.Clear();

                List<PendingRejection>? pendingRejections = null;
                int lastYieldedIndex = 0;

                await foreach (ChatResponseUpdate update in
                    base.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
                {
                    pendingRejections = await CollectRejectionsAsync(update.Contents, pendingRejections, cancellationToken);
                    updates.Add(update);

                    if (pendingRejections is null)
                    {
                        lastYieldedIndex++;
                        yield return update;
                    }
                }

                if (pendingRejections is not { Count: > 0 })
                {
                    for (; lastYieldedIndex < updates.Count; lastYieldedIndex++)
                        yield return updates[lastYieldedIndex];
                        
                    yield break;
                }

                var rejectedCallIds = new HashSet<string>(pendingRejections.Select(r => r.Call.CallId));
                ChatResponse preResponse = updates.ToChatResponse();

                if (HasNonRejectedFunctionCallOrApproval(preResponse, rejectedCallIds))
                {
                    for (; lastYieldedIndex < updates.Count; lastYieldedIndex++)
                        yield return updates[lastYieldedIndex];
                        
                    yield break;
                }

                var rejections = ApplyRejections(pendingRejections);
                var addedMessage = CreateRejectionMessage(rejections);
                (addedMessages ??= []).Add(addedMessage);

                var addedUpdate = ConvertToolResultMessageToUpdate(
                    addedMessage, options?.ConversationId, addedMessage.MessageId);

                updates.Add(addedUpdate);
                yield return addedUpdate;

                ChatResponse responseWithRejections = updates.ToChatResponse();

                FixupHistories(originalMessages, ref chatMessages, ref augmentedMessages,
                    responseWithRejections, responseMessages, addedMessages, ref lastIterationHadConversationId);

                responseMessages.AddRange(responseWithRejections.Messages);

                UpdateOptionsForNextIteration(ref options, responseWithRejections.ConversationId);

                updates.Clear();
            }
        }

        private record struct PendingRejection(
            IList<AIContent> Contents, int Index,
            FunctionCallContent Call, string RejectionMessage);

        private async ValueTask<List<PendingRejection>?> CollectRejectionsAsync(
            IList<AIContent> contents, List<PendingRejection>? pending, CancellationToken cancellationToken)
        {
            for (var i = 0; i < contents.Count; i++)
            {
                FunctionCallContent? call = GetFunctionCallContent(contents[i]);

                if (call is null)
                    continue;

                string? rejection = await FunctionCallPreValidator.PreValidateAsync(call, cancellationToken);

                if (rejection is not null)
                {
                    (pending ??= []).Add(new(contents, i, call, rejection));
                }
            }

            return pending;
        }

        private static List<AIContent> ApplyRejections(List<PendingRejection> pending)
        {
            List<AIContent> rejections = new(pending.Count);

            foreach (var (contents, index, call, rejectionMessage) in pending)
            {
                call.InformationalOnly = true;
                contents[index] = call;
                rejections.Add(new FunctionResultContent(call.CallId, rejectionMessage)
                {
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        [KeyPreValidationRejected] = true
                    }
                });
            }

            return rejections;
        }

        private static ChatMessage CreateRejectionMessage(List<AIContent> rejections) => new(ChatRole.Tool, rejections)
        {
            MessageId = $"PreValidation:{Guid.NewGuid():N}"
        };

        private static FunctionCallContent? GetFunctionCallContent(AIContent content) => content switch
        {
            FunctionCallContent { InformationalOnly: false } call => call,
            ToolApprovalRequestContent { ToolCall: FunctionCallContent call } => call,
            _ => null
        };

        private static bool HasNonRejectedFunctionCallOrApproval(
            ChatResponse response, HashSet<string> rejectedCallIds)
        {
            return response.Messages.SelectMany(m => m.Contents).Any(c => c switch
            {
                FunctionCallContent { InformationalOnly: false } call
                    => !rejectedCallIds.Contains(call.CallId),
                ToolApprovalRequestContent { ToolCall: FunctionCallContent call }
                    => !rejectedCallIds.Contains(call.CallId),
                _ => false
            });
        }

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
