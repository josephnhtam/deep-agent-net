using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class FileOverwriteToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly IFileLocks _fileLocks;

        public AITool Tool { get; }

        internal FileOverwriteToolProvider(IFileSystemAccess access, ToolOptions options, IFileLocks fileLocks)
        {
            _access = access;
            _fileLocks = fileLocks;

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
                    return $"Successfully overwrote '{filePath}'";
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            }
        }

        public static async ValueTask<string?> ValidateAsync(
            string filePath, IFileSystemAccess access, CancellationToken cancellationToken)
            => await FileToolGuards.ValidateReadStateAsync(filePath, access, cancellationToken);
    }
}
