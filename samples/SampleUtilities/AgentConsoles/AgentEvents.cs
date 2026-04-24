using DeepAgentNet.TodoLists;
using Microsoft.Extensions.AI;

namespace SampleUtilities.AgentConsoles
{
    public abstract record AgentEvent;
    public record AgentTextDelta(string Text) : AgentEvent;
    public record AgentThinkingDelta(string Text) : AgentEvent;
    public record AgentNewMessage(string MessageId) : AgentEvent;
    public record TodosUpdated(string AgentId, List<Todo> Todos) : AgentEvent;
    public record ToolUsed(string AgentId, FunctionCallContent Call, FunctionResultContent Result) : AgentEvent;
    public record SubAgentStarted(string AgentId, string TaskId, string Description) : AgentEvent;
    public record SubAgentCompleted(string AgentId, string TaskId, string Description) : AgentEvent;
    public record ApprovalRequired(string AgentId, ToolApprovalRequestContent Request, TaskCompletionSource<ToolApprovalResponseContent> Completion) : AgentEvent;
    public record AgentTurnComplete : AgentEvent;
}
