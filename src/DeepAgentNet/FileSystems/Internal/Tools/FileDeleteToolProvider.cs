using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class FileDeleteToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly IFileLocks _fileLocks;
        private readonly ILogger<FileDeleteToolProvider>? _logger;

        public AITool Tool { get; }

        public FileDeleteToolProvider(IFileSystemAccess access, ToolOptions options, IFileLocks fileLocks, ILoggerFactory? loggerFactory = null)
        {
            _access = access;
            _fileLocks = fileLocks;
            _logger = loggerFactory?.CreateLogger<FileDeleteToolProvider>();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.DeleteFileToolName,
                Description = options.Description ?? FileSystemDefaults.DeleteFileToolDescription,
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
            [Description("The path to the file to delete")]
            string filePath,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            filePath = await _access.ResolvePathAsync(filePath, cwdPath, cancellationToken).ConfigureAwait(false);

            _logger?.DeletingFile(filePath);

            using (await _fileLocks.AcquireAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                string? validationError = await ValidateAsync(filePath, _access, cancellationToken)
                    .ConfigureAwait(false);

                if (validationError is not null)
                    return validationError;

                try
                {
                    await _access.DeleteAsync(filePath, cancellationToken).ConfigureAwait(false);

                    _logger?.DeleteFileCompleted(filePath);

                    return $"Successfully deleted '{filePath}'";
                }
                catch (Exception ex)
                {
                    _logger?.DeleteFileFailed(ex, filePath);

                    return $"Error: {ex.Message}";
                }
            }
        }

        public static async ValueTask<string?> ValidateAsync(
            string filePath, IFileSystemAccess access, CancellationToken cancellationToken)
            => await FileToolGuards.ValidateReadStateAsync(filePath, access, cancellationToken);
    }
}
