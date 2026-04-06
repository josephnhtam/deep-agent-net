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
        private readonly FileLocks _fileLocks;

        public AITool Tool { get; }

        internal FileEditToolProvider(IFileSystemAccess access, ToolOptions options, FileLocks fileLocks)
        {
            _access = access;
            _fileLocks = fileLocks;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.EditFileToolName,
                Description = options.Description ?? FileSystemDefaults.EditFileToolDescription
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private async ValueTask<string> ExecuteAsync(
            [Description("The path to the file to edit")]
            string filePath,
            [Description("The text to replace")]
            string oldString,
            [Description("The text to replace it with")]
            string newString,
            [Description("Replace all occurrences of oldString")]
            bool replaceAll,
            CancellationToken cancellationToken = default)
        {
            if (oldString == newString)
                return "No changes to make: oldString and newString are exactly the same.";

            using (await _fileLocks.AcquireAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                string? validationError = FileToolGuards.ValidateReadState(filePath, _access);

                if (validationError is not null)
                    return validationError;

                try
                {
                    EditResult result = await _access.EditAsync(filePath, oldString, newString, replaceAll, cancellationToken).ConfigureAwait(false);
                    FileToolGuards.UpdateReadState(filePath, _access);

                    return replaceAll
                        ? $"The file '{filePath}' has been updated. All occurrences were successfully replaced."
                        : $"The file '{filePath}' has been updated successfully.";
                }
                catch (Exception ex)
                {
                    return $"""
                        Error: {ex.Message}

                        Please read the file again before editing.
                        Ensure that the oldString does not contain the line number prefixes (a padded line number followed by an arrow (→)) added by the read_file tool.
                        """;
                }
            }
        }
    }
}
