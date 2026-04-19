using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class FileWriteToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly IFileLocks _fileLocks;
        private readonly ILogger<FileWriteToolProvider>? _logger;

        public AITool Tool { get; }

        internal FileWriteToolProvider(IFileSystemAccess access, ToolOptions options, IFileLocks fileLocks, ILoggerFactory? loggerFactory = null)
        {
            _access = access;
            _fileLocks = fileLocks;
            _logger = loggerFactory?.CreateLogger<FileWriteToolProvider>();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.WriteFileToolName,
                Description = options.Description ?? FileSystemDefaults.WriteFileToolDescription,
                JsonSchemaCreateOptions = CreateJsonSchemaOptions()
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private AIJsonSchemaCreateOptions CreateJsonSchemaOptions() => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "cwdPath" => $"The working directory for resolving relative paths. Defaults to '{_access.RootWorkingDirectory}'.",
                _ => null
            }
        };

        private async ValueTask<string> ExecuteAsync(
            [Description("The path to the file to write")]
            string filePath,
            [Description("Content to write to the file")]
            string content,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            filePath = await _access.ResolvePathAsync(filePath, cwdPath, cancellationToken).ConfigureAwait(false);

            _logger?.WritingFile(filePath);

            using (await _fileLocks.AcquireAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                string? validationError = await ValidateAsync(filePath, _access, cancellationToken)
                    .ConfigureAwait(false);

                if (validationError is not null)
                    return validationError;

                try
                {
                    await _access.WriteAsync(filePath, content, cancellationToken).ConfigureAwait(false);

                    _logger?.WriteFileCompleted(filePath);

                    return $"Successfully wrote to '{filePath}'";
                }
                catch (Exception ex)
                {
                    _logger?.WriteFileFailed(ex, filePath);

                    return $"Error: {ex.Message}";
                }
            }
        }

        public static ValueTask<string?> ValidateAsync(
            string filePath, IFileSystemAccess access, CancellationToken cancellationToken)
            => FileToolGuards.ValidateLsState(filePath, access, cancellationToken);
    }
}
