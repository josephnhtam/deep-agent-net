using DeepAgentNet.Shared;
using DeepAgentNet.Shells.Contracts;

namespace DeepAgentNet.Shells
{
    public record ShellProviderOptions(IShellResolver ShellResolver)
    {
        public string? DefaultWorkingDirectory { get; init; }
        public string? SystemPrompt { get; init; }
        public Func<IReadOnlyList<string>, string>? Description { get; init; } = null;
        public ToolApprovalPolicy ApprovalPolicy { get; init; } = ToolApprovalPolicy.Required;
    }
}
