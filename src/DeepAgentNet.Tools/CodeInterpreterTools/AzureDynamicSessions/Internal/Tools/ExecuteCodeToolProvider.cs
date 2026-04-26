using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients;
using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Tools
{
    internal class ExecuteCodeToolProvider
    {
        private readonly IAzureDynamicSessionsClient _client;
        private readonly IFileSystemAccess _fileSystemAccess;
        private readonly string _sessionId;
        private readonly int? _maxChars;

        public AITool Tool { get; }

        public ExecuteCodeToolProvider(
            IAzureDynamicSessionsClient client,
            IFileSystemAccess fileSystemAccess,
            string sessionId,
            string language = "Python",
            TokenLimitedToolOptions? options = null)
        {
            _client = client;
            _fileSystemAccess = fileSystemAccess;
            _sessionId = sessionId;
            _maxChars = options?.ResultTokenLimit * SharedConstants.ApproximateCharsPerToken;

            options ??= new();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = AzureDynamicSessionsDefaults.ExecuteCodeToolName,
                Description = options.Description ?? AzureDynamicSessionsDefaults.GetExecuteCodeToolDescription(language),
                JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions
                {
                    ParameterDescriptionProvider = property => property.Name switch
                    {
                        "cwdPath" => $"The working directory for resolving relative local file paths. Defaults to '{fileSystemAccess.RootWorkingDirectory}'.",
                        _ => null
                    }
                }
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required
                ? new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<ExecuteCodeResult> ExecuteAsync(
            [Description("The code to execute in the session")]
            string code,
            [Description("Optional map of local file paths to target file names. Each file is uploaded to /mnt/data/{fileName} in the session before code execution.")]
            Dictionary<string, string>? files = null,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            if (files is not null)
            {
                foreach ((string localFilePath, string fileName) in files)
                {
                    string resolvedPath = await _fileSystemAccess
                        .ResolvePathAsync(localFilePath, cwdPath, cancellationToken)
                        .ConfigureAwait(false);

                    await using Stream stream = await _fileSystemAccess
                        .ReadDataAsync(resolvedPath, cancellationToken)
                        .ConfigureAwait(false);

                    await _client.UploadFileAsync(_sessionId, fileName, stream, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            CodeExecutionResult result = await _client
                .ExecuteCodeAsync(_sessionId, code, cancellationToken)
                .ConfigureAwait(false);

            string? stdout = result.Stdout;
            string? stderr = result.Stderr;
            string? executionResult = result.ExecutionResult?.ToString();
            bool truncated = false;

            if (_maxChars.HasValue)
            {
                stdout = Truncate(stdout, _maxChars.Value, ref truncated);
                stderr = Truncate(stderr, _maxChars.Value, ref truncated);
                executionResult = Truncate(executionResult, _maxChars.Value, ref truncated);
            }

            return new ExecuteCodeResult
            {
                SessionId = _sessionId,
                Status = result.Status,
                Stdout = stdout,
                Stderr = stderr,
                ExecutionResult = executionResult,
                ExecutionTimeInMilliseconds = result.ExecutionTimeInMilliseconds,
                IsTruncated = truncated
            };
        }

        private static string? Truncate(string? value, int maxChars, ref bool truncated)
        {
            if (value is null || value.Length <= maxChars)
                return value;

            truncated = true;
            return string.Concat(value.AsSpan(0, maxChars), "... [output truncated]");
        }

        private record ExecuteCodeResult
        {
            [Description("The session identifier for this execution")]
            public required string SessionId { get; init; }

            [Description("The execution status")]
            public required string Status { get; init; }

            [Description("The standard output from the execution")]
            public string? Stdout { get; init; }

            [Description("The standard error output from the execution")]
            public string? Stderr { get; init; }

            [Description("The result value of the last expression, if any")]
            public string? ExecutionResult { get; init; }

            [Description("The execution time in milliseconds")]
            public long? ExecutionTimeInMilliseconds { get; init; }

            [Description("Whether any output field was truncated due to size limits")]
            public bool IsTruncated { get; init; }
        }
    }
}
