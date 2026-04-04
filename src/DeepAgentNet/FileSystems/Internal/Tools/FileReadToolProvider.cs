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
            [Description("Maximum number of lines to read")]
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            try
            {
                IStringBuilder sb = _options.ResultTokenLimit.HasValue ?
                    new TruncatingStringBuilder(
                        _options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken,
                        SharedConstants.TruncationGuidance) :
                    new StandardStringBuilder();

                await foreach (string line in _access.ReadAsync(filePath, offset, limit, cancellationToken).ConfigureAwait(false))
                {
                    if (!sb.AppendLine(line))
                        break;
                }

                string result = sb.ToString();

                if (string.IsNullOrEmpty(result) && offset == 0 && limit > 0)
                    return "[System: This file exists but has empty contents]";

                return result;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
