using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class FileOverwriteToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly IFileLocks _fileLocks;
        private readonly ILogger<FileOverwriteToolProvider>? _logger;

        public AITool Tool { get; }

        internal FileOverwriteToolProvider(IFileSystemAccess access, ToolOptions options, IFileLocks fileLocks, ILoggerFactory? loggerFactory = null)
        {
            _access = access;
            _fileLocks = fileLocks;
            _logger = loggerFactory?.CreateLogger<FileOverwriteToolProvider>();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.OverwriteFileToolName,
                Description = options.Description ?? FileSystemDefaults.OverwriteFileToolDescription,
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
            [Description("The path to the file to overwrite")]
            string filePath,
            [Description("Content to replace the file with")]
            string content,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            filePath = await _access.ResolvePathAsync(filePath, cwdPath, cancellationToken).ConfigureAwait(false);

            _logger?.OverwritingFile(filePath);

            using (await _fileLocks.AcquireAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                string? validationError = await ValidateAsync(filePath, _access, cancellationToken)
                    .ConfigureAwait(false);

                if (validationError is not null)
                    return validationError;

                try
                {
                    await _access.OverwriteAsync(filePath, content, cancellationToken).ConfigureAwait(false);
                    await FileToolGuards.UpdateReadStateAsync(filePath, _access, cancellationToken);

                    _logger?.OverwriteFileCompleted(filePath);

                    return $"Successfully overwrote '{filePath}'";
                }
                catch (Exception ex)
                {
                    _logger?.OverwriteFileFailed(ex, filePath);

                    return $"Error: {ex.Message}";
                }
            }
        }

        public static async ValueTask<string?> ValidateAsync(
            string filePath, IFileSystemAccess access, CancellationToken cancellationToken)
            => await FileToolGuards.ValidateReadStateAsync(filePath, access, cancellationToken);
    }
}
