using DeepAgentNet.SubAgents.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CodingAgentSample
{
    internal class SubAgentHandle(ChannelWriter<AgentEvent> channel) : ISubAgentHandle
    {
        private readonly ConcurrentDictionary<string, string> _taskDescriptions = new();
        private readonly ConcurrentDictionary<string, FunctionCallContent> _functionCalls = new();

        public Task<ToolApprovalResponseContent> ApproveToolCallAsync(
            string agentId, ToolApprovalRequestContent call, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<ToolApprovalResponseContent>();
            channel.TryWrite(new ApprovalRequired(agentId, call, tcs));
            return tcs.Task;
        }

        public Task<object?> ProvideFunctionResultAsync(
            string agentId, FunctionCallContent call, CancellationToken cancellationToken)
        {
            return Task.FromResult<object?>(null);
        }

        public ValueTask ReceiveUpdateAsync(
            string agentId, AgentResponseUpdate update, CancellationToken cancellationToken)
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
                    channel.TryWrite(new ToolUsed(agentId, call, callResult));
                }
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask OnSessionCreateOrResumedAsync(
            string agentId, string taskId, bool resumed, string description, string prompt,
            CancellationToken cancellationToken)
        {
            _taskDescriptions[taskId] = description;
            channel.TryWrite(new SubAgentStarted(agentId, taskId, description));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnSessionCompletedAsync(
            string agentId, string taskId, CancellationToken cancellationToken)
        {
            _taskDescriptions.TryRemove(taskId, out var desc);
            channel.TryWrite(new SubAgentCompleted(agentId, taskId, desc ?? string.Empty));
            return ValueTask.CompletedTask;
        }
    }
}
