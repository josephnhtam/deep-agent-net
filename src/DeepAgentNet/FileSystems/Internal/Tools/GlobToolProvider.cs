using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Contracts;
using DeepAgentNet.Shared.Internal;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using FileSystemInfo = DeepAgentNet.FileSystems.Contracts.FileSystemInfo;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
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

                IStringBuilder sb = _options.ResultTokenLimit.HasValue ?
                    new TruncatingStringBuilder(
                        _options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken,
                        SharedConstants.TruncationGuidance) :
                    new StandardStringBuilder();

                bool hasEntries = false;

                await foreach (FileSystemInfo info in _access.GlobInfoAsync(pattern, path, cancellationToken).ConfigureAwait(false))
                {
                    hasEntries = true;

                    string line = $"{info.Path} ({info.Size} bytes)";

                    if (!sb.AppendLine(line))
                        break;
                }

                return hasEntries ? sb.ToString() : $"No files found matching pattern '{pattern}'";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
