using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions
{
    public record AzureDynamicSessionsProviderOptions(
        AzureDynamicSessionsOptions DynamicSessionOptions,
        IFileSystemAccess FileSystemAccess)
    {
        public Func<AzureDynamicSessionsInfo, string>? SystemPrompt { get; init; }
        public bool FetchPreinstalledPackages { get; init; } = true;
        public TokenLimitedToolOptions ExecuteCodeToolOptions { get; init; } = new();
        public ToolOptions DownloadFileToolOptions { get; init; } = new();
        public ToolOptions ListFilesToolOptions { get; init; } = new();
    }
}
