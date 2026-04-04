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

    internal class GlobToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly TokenLimitedToolOptions _options;

        public AITool Tool { get; }

        public GlobToolProvider(IFileSystemAccess access, TokenLimitedToolOptions options)
        {
            _access = access;
            _options = options;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.GlobToolName,
                Description = options.Description ?? FileSystemDefaults.GlobToolDescription
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<string> ExecuteAsync(
            [Description("Glob pattern (e.g., '*.py', '**/*.ts')")]
            string pattern,
            [Description("Base path to search from (default: /)")]
            string path = "/",
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    path = "/";

                List<FileSystemInfo> globInfo = await _access.GlobInfoAsync(pattern, path, cancellationToken).ConfigureAwait(false);

                if (!globInfo.Any())
                    return $"No files found matching pattern '{pattern}'";

                IStringBuilder sb = _options.ResultTokenLimit.HasValue ?
                    new TruncatingStringBuilder(
                        _options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken,
                        SharedConstants.TruncationGuidance) :
                    new StandardStringBuilder();

                foreach (FileSystemInfo info in globInfo.Where(i => !i.IsDirectory))
                {
                    string line = $"{info.Path} ({info.Size} bytes)";

                    if (!sb.AppendLine(line))
                        break;
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
