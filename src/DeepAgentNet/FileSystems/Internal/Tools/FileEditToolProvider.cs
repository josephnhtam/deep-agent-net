using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    public class FileEditToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;

        public AITool Tool { get; }

        public FileEditToolProvider(IFileSystemAccess access, ToolOptions options)
        {
            _access = access;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.EditFileToolName,
                Description = options.Description ?? FileSystemDefaults.EditFileToolDescription
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<string> ExecuteAsync(
            [Description("Path to the file to edit")]
            string filePath,
            [Description("String to be replaced (must match exactly)")]
            string oldString,
            [Description("String to replace with")]
            string newString,
            [Description("Whether to replace all occurrences")]
            bool replaceAll,
            CancellationToken cancellationToken = default)
        {
            try
            {
                EditResult result = await _access.EditAsync(filePath, oldString, newString, replaceAll, cancellationToken).ConfigureAwait(false);
                return $"Successfully replaced {result.Occurrences} occurrence(s) in '{filePath}'";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
