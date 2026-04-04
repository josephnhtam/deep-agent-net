using DeepAgentNet.FileSystems.Contracts;
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
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    path = "/";

                List<FileSystemInfo> lsInfo = await _access.ListInfoAsync(path, cancellationToken).ConfigureAwait(false);

                if (!lsInfo.Any())
                    return $"No files found in {path}";

                IStringBuilder sb = _options.ResultTokenLimit.HasValue ?
                    new TruncatingStringBuilder(
                        _options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken,
                        SharedConstants.TruncationGuidance) :
                    new StandardStringBuilder();

                foreach (FileSystemInfo info in lsInfo)
                {
                    string line = info switch
                    {
                        { IsDirectory: true } => $"{info.Path} (directory)",
                        { IsDirectory: false, Size: > 0 } => $"{info.Path} ({info.Size} bytes)",
                        _ => info.Path
                    };

                    if (!sb.AppendLine(line))
                        break;
                }

                var result = sb.ToString();
                return result;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
