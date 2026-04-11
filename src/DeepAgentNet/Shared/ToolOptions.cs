namespace DeepAgentNet.Shared
{
    public record ToolOptions
    {
        public string? Description { get; init; } = null;
        public ToolApprovalPolicy ApprovalPolicy { get; init; } = ToolApprovalPolicy.NotRequired;
    }

    public record TokenLimitedToolOptions : ToolOptions
    {
        public int? ResultTokenLimit { get; init; } = 100_000;
    }

    public record ReadFileDataToolOptions : ToolOptions
    {
        public long MaxBytes { get; init; } = 10_000_000;
    }
}
