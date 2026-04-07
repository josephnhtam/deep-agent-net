using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class FileDeleteToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly IFileLocks _fileLocks;

        public AITool Tool { get; }

        public FileDeleteToolProvider(IFileSystemAccess access, ToolOptions options, IFileLocks fileLocks)
        {
            _access = access;
            _fileLocks = fileLocks;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.DeleteFileToolName,
                Description = options.Description ?? FileSystemDefaults.DeleteFileToolDescription
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<string> ExecuteAsync(
            [Description("Path to the file to delete")]
            string filePath,
            CancellationToken cancellationToken = default)
        {
            using (await _fileLocks.AcquireAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                string? validationError = await ValidateAsync(filePath, _access, cancellationToken)
                    .ConfigureAwait(false);

                if (validationError is not null)
                    return validationError;

                try
                {
                    await _access.DeleteAsync(filePath, cancellationToken).ConfigureAwait(false);
                    return $"Successfully deleted '{filePath}'";
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            }
        }

        public static ValueTask<string?> ValidateAsync(
            string filePath, IFileSystemAccess access, CancellationToken cancellationToken)
            => new(FileToolGuards.ValidateReadState(filePath, access));
    }
}
