using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Contracts;
using DeepAgentNet.Shared.Internal;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class FileReadToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly TokenLimitedToolOptions _options;

        public AITool Tool { get; }

        public FileReadToolProvider(IFileSystemAccess access, TokenLimitedToolOptions options)
        {
            _access = access;
            _options = options;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.ReadFileToolName,
                Description = options.Description ?? FileSystemDefaults.ReadFileToolDescription
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<string> ExecuteAsync(
            [Description("Path to the file to read")]
            string filePath,
            [Description("Line offset to start reading from (0-indexed)")]
            int offset = 0,
            [Description("Maximum number of lines to read (defaults to 500)")]
            int limit = 500,
            CancellationToken cancellationToken = default)
        {
            try
            {
                TruncatingStringBuilder? truncatingBuilder = _options.ResultTokenLimit.HasValue
                    ? new TruncatingStringBuilder(
                        _options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken,
                        SharedConstants.TruncationGuidance)
                    : null;

                IStringBuilder sb = truncatingBuilder ?? (IStringBuilder)new StandardStringBuilder();

                int linesRead = 0;
                bool hasMore = false;

                await foreach (string line in _access.ReadAsync(filePath, offset, limit + 1, cancellationToken).ConfigureAwait(false))
                {
                    if (linesRead >= limit)
                    {
                        hasMore = true;
                        break;
                    }

                    if (!sb.AppendLine(line))
                        break;

                    linesRead++;
                }

                if (linesRead == 0 && offset == 0)
                    return "[System: This file exists but has empty contents]";

                bool tokenTruncated = truncatingBuilder?.IsTruncated ?? false;
                int startLine = offset + 1;
                int endLine = offset + linesRead;

                string result = sb.ToString();

                if (tokenTruncated || hasMore)
                    result += $"\n\n(Showing lines {startLine}-{endLine}. Use offset={endLine} to continue reading.)";
                else
                    result += $"\n\n(End of file. Showing lines {startLine}-{endLine})";

                return result;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
