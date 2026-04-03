using DeepAgentNet.Shared;

namespace DeepAgentNet.FileSystems
{
    public record FileSystemProviderOptions
    {
        public string? SystemPrompt { get; init; }
        public FileSystemAccessPermissionOptions Permissions { get; init; } = new();
    }

    public record FileSystemAccessPermissionOptions
    {
        public ToolApprovalPolicy Read { get; init; } = ToolApprovalPolicy.NotRequired;
        public ToolApprovalPolicy Write { get; init; } = ToolApprovalPolicy.Required;
    }
}
