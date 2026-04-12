using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class FileWriteToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly IFileLocks _fileLocks;

        public AITool Tool { get; }

        internal FileWriteToolProvider(IFileSystemAccess access, ToolOptions options, IFileLocks fileLocks)
        {
            _access = access;
            _fileLocks = fileLocks;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.WriteFileToolName,
                Description = options.Description ?? FileSystemDefaults.WriteFileToolDescription
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<string> ExecuteAsync(
            [Description("The absoulte path to the file to write")]
            string filePath,
            [Description("Content to write to the file")]
            string content,
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
                    await _access.WriteAsync(filePath, content, cancellationToken).ConfigureAwait(false);
                    return $"Successfully wrote to '{filePath}'";
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            }
        }

        public static ValueTask<string?> ValidateAsync(
            string filePath, IFileSystemAccess access, CancellationToken cancellationToken)
            => new(FileToolGuards.ValidateLsState(filePath));
    }
}
