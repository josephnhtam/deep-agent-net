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

        private async ValueTask<Result> ExecuteAsync(
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
                    ? new TruncatingStringBuilder(_options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken)
                    : null;

                IStringBuilder sb = truncatingBuilder ?? (IStringBuilder)new StandardStringBuilder();

                int linesRead = 0;
                int totalLines = offset;

                await foreach (string line in _access.ReadAsync(filePath, offset, null, cancellationToken).ConfigureAwait(false))
                {
                    totalLines++;

                    string lineNum = totalLines.ToString();
                    string prefix = lineNum.Length >= 6 ? lineNum : lineNum.PadLeft(6);

                    if (linesRead < limit && sb.AppendLine($"{prefix}\u2192{line}"))
                    {
                        linesRead++;
                    }
                }

                int startLine = offset + 1;
                string content = sb.ToString();

                await FileToolGuards.RecordFileReadAsync(filePath, _access, cancellationToken);

                return new Result
                {
                    FilePath = filePath,
                    Content = content,
                    NumLines = linesRead,
                    StartLine = startLine,
                    TotalLines = totalLines
                };
            }
            catch (Exception ex)
            {
                return new Result
                {
                    FilePath = filePath,
                    Error = ex.Message
                };
            }
        }


        private record Result
        {
            [Description("Path to the file that was read")]
            public required string FilePath { get; init; }

            [Description("The error message if an error occurred, otherwise null")]
            public string? Error { get; init; }

            [Description("The content of the file, each line prefixed with its line number and an arrow (→)")]
            public string? Content { get; init; }

            [Description("The number of lines returned in Content (not including truncation)")]
            public int? NumLines { get; init; }

            [Description("The line number that was read from in the file (1-indexed)")]
            public int? StartLine { get; init; }

            [Description("The total number of lines in the file")]
            public int? TotalLines { get; init; }
        }
    }
}
