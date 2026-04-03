namespace DeepAgentNet.Shared
{
    public record ToolOptions(string? Description = null, ToolApprovalPolicy ApprovalPolicy = ToolApprovalPolicy.NotRequired);
}
