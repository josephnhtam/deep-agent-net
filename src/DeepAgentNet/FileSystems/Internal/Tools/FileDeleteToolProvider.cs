using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    public class FileDeleteToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;

        public AITool Tool { get; }

        public FileDeleteToolProvider(IFileSystemAccess access, ToolOptions options)
        {
            _access = access;

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
}
