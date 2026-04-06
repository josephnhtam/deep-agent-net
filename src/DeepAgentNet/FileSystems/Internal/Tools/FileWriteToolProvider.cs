using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    public class FileWriteToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly FileLocks _fileLocks;

        public AITool Tool { get; }

        internal FileWriteToolProvider(IFileSystemAccess access, ToolOptions options, FileLocks fileLocks)
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
            [Description("Path to the file to write")]
            string filePath,
            [Description("Content to write to the file")]
            string content,
            CancellationToken cancellationToken = default)
        {
            using (await _fileLocks.AcquireAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                string? validationError = FileToolGuards.ValidateLsState(filePath);

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
    }
}
