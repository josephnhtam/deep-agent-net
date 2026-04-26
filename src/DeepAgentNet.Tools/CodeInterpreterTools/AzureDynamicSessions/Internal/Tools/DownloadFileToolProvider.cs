using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Tools
{
    internal class DownloadFileToolProvider
    {
        private readonly IAzureDynamicSessionsClient _client;
        private readonly IFileSystemAccess _fileSystemAccess;
        private readonly string _sessionId;

        public AITool Tool { get; }

        public DownloadFileToolProvider(
            IAzureDynamicSessionsClient client,
            IFileSystemAccess fileSystemAccess,
            string sessionId,
            ToolOptions? options = null)
        {
            _client = client;
            _fileSystemAccess = fileSystemAccess;
            _sessionId = sessionId;

            options ??= new();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = AzureDynamicSessionsDefaults.DownloadFileToolName,
                Description = options.Description ?? AzureDynamicSessionsDefaults.DownloadFileToolDescription,
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

        private async ValueTask<DownloadFileResult> ExecuteAsync(
            [Description("The file name in the session's /mnt/data directory to download")]
            string fileName,
            [Description("The local file path to save the downloaded file to")]
            string filePath,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            filePath = await _fileSystemAccess.ResolvePathAsync(filePath, cwdPath, cancellationToken)
                .ConfigureAwait(false);

            await using Stream downloadStream = await _client
                .DownloadFileAsync(_sessionId, fileName, cancellationToken)
                .ConfigureAwait(false);

            await _fileSystemAccess.WriteAsync(filePath, stream =>
                    downloadStream.CopyToAsync(stream, cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            return new DownloadFileResult
            {
                FilePath = filePath,
                FileName = fileName
            };
        }

        private record DownloadFileResult
        {
            [Description("The local file path where the file was saved")]
            public required string FilePath { get; init; }

            [Description("The file name that was downloaded from the session's /mnt/data")]
            public required string FileName { get; init; }
        }
    }
}
