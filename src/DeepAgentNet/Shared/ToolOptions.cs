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

    public record DataLimitedToolOptions : ToolOptions
    {
        public long ResultBytesLimit { get; init; } = 320_000_000;
    }
}
