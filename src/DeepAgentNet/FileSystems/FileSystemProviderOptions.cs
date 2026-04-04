using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;

namespace DeepAgentNet.FileSystems
{
    public record FileSystemProviderOptions(IFileSystemAccess Access)
    {
        public string? SystemPrompt { get; init; }
        public TokenLimitedToolOptions LsToolOptions { get; init; } = new();
        public TokenLimitedToolOptions ReadFileToolOptions { get; init; } = new();
        public TokenLimitedToolOptions GlobToolOptions { get; init; } = new();
        public TokenLimitedToolOptions GrepToolOptions { get; init; } = new();
        public ToolOptions WriteFileToolOptions { get; init; } = new() { ApprovalPolicy = ToolApprovalPolicy.Required };
        public ToolOptions EditFileToolOptions { get; init; } = new() { ApprovalPolicy = ToolApprovalPolicy.Required };
    }
}
