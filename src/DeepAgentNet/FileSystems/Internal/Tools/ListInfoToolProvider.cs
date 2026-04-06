using DeepAgentNet.Agents.Internal;
using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Contracts;
using DeepAgentNet.Shared.Internal;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    using FileSystemInfo = Contracts.FileSystemInfo;

    internal class ListInfoToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly TokenLimitedToolOptions _options;

        public AITool Tool { get; }

        public ListInfoToolProvider(IFileSystemAccess access, TokenLimitedToolOptions options)
        {
            _access = access;
            _options = options;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.LsToolName,
                Description = options.Description ?? FileSystemDefaults.LsToolDescription
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<string> ExecuteAsync(
            [Description("Directory path to list (default: /)")]
            string path = "/",
            [Description("Whether to list recursively (default: false)")]
            bool recursive = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    path = "/";

                IStringBuilder sb = _options.ResultTokenLimit.HasValue ?
                    new TruncatingStringBuilder(
                        _options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken,
                        SharedConstants.TruncationGuidance) :
                    new StandardStringBuilder();

                bool hasEntries = false;
                string basePath = path.TrimEnd('/');
                LsState? lsState = GetOrCreateLsState();

                lsState?.Record(basePath);

                await foreach (FileSystemInfo info in
                    _access.ListInfoAsync(path, recursive, cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    hasEntries = true;

                    if (info.IsDirectory)
                        lsState?.Record(Path.Combine(basePath, info.Path));

                    string line = info switch
                    {
                        { IsDirectory: true } => $"{info.Path} (directory)",
                        { IsDirectory: false, Size: > 0 } => $"{info.Path} ({info.Size} bytes)",
                        _ => info.Path
                    };

                    if (!sb.AppendLine(line))
                        break;
                }

                return hasEntries ? sb.ToString() : $"No files found in {path}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private static LsState? GetOrCreateLsState()
        {
            var session = FunctionInvokingChatClient.CurrentContext?.Options?.GetSession();
            if (session is null)
                return null;

            var state = session.StateBag.GetValue<LsState>(LsState.StateBagKey);
            if (state is null)
            {
                state = new LsState();
                session.StateBag.SetValue(LsState.StateBagKey, state);
            }

            return state;
        }
    }
}
