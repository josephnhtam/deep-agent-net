using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CodingAgentSample
{
    public interface IAgentTurnRunner
    {
        Task RunAsync(string userInput, CancellationToken cancellationToken);
    }

    public class AgentTurnRunner : IAgentTurnRunner
    {
        private readonly AIAgent _agent;
        private readonly AgentSession _session;
        private readonly ChannelWriter<AgentEvent> _channel;
        private readonly ConcurrentDictionary<string, FunctionCallContent> _functionCalls = new();

        public AgentTurnRunner(AIAgent agent, AgentSession session, ChannelWriter<AgentEvent> channel)
        {
            _agent = agent;
            _session = session;
            _channel = channel;
        }

        public async Task RunAsync(string userInput, CancellationToken cancellationToken)
        {
            try
            {
                var inputs = new List<ChatMessage>
                {
                    new(ChatRole.User, userInput)
                };

                while (true)
                {
                    var updates = new List<AgentResponseUpdate>();
                    string? lastMsgId = null;

                    await foreach (var update in _agent.RunStreamingAsync(inputs, _session, cancellationToken: cancellationToken))
                    {
                        foreach (var call in update.Contents.OfType<FunctionCallContent>())
                        {
                            if (call.Name != "write_todos")
                                _functionCalls[call.CallId] = call;
                        }

                        foreach (var callResult in update.Contents.OfType<FunctionResultContent>())
                        {
                            var callId = callResult.CallId;

                            if (_functionCalls.TryRemove(callId, out var call) &&
                                !callResult.IsRejectedFunctionResult())
                            {
                                _channel.TryWrite(new ToolUsed(_agent.Id, call, callResult));
                            }
                        }

                        foreach (var reasoning in update.Contents.OfType<TextReasoningContent>())
                        {
                            if (!string.IsNullOrEmpty(reasoning.Text))
                                _channel.TryWrite(new AgentThinkingDelta(reasoning.Text));
                        }

                        if (!string.IsNullOrEmpty(update.Text))
                        {
                            if (update.MessageId != lastMsgId)
                            {
                                lastMsgId = update.MessageId;
                                _channel.TryWrite(new AgentNewMessage(update.MessageId!));
                            }

                            _channel.TryWrite(new AgentTextDelta(update.Text));
                        }

                        updates.Add(update);
                    }

                    var response = updates.ToAgentResponse();

                    var approvals = response.Messages
                        .SelectMany(m => m.Contents)
                        .OfType<ToolApprovalRequestContent>()
                        .ToList();

                    if (approvals.Count == 0)
                        break;

                    var results = new List<AIContent>();

                    foreach (var approval in approvals)
                    {
                        var responseSource = new TaskCompletionSource<ToolApprovalResponseContent>();
                        _channel.TryWrite(new ApprovalRequired(_agent.Id, approval, responseSource));
                        results.Add(await responseSource.Task);
                    }

                    inputs =
                    [
                        new ChatMessage(ChatRole.Tool, results)
                        {
                            MessageId = $"approval:{Guid.NewGuid():N}"
                        }
                    ];
                }
            }
            finally
            {
                _channel.TryWrite(new AgentTurnComplete());
            }
        }

    }
}
