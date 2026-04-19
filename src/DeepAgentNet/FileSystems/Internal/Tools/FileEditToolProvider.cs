using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class FileEditToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly IFileLocks _fileLocks;

        public AITool Tool { get; }

        internal FileEditToolProvider(IFileSystemAccess access, ToolOptions options, IFileLocks fileLocks)
        {
            _access = access;
            _fileLocks = fileLocks;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.EditFileToolName,
                Description = options.Description ?? FileSystemDefaults.EditFileToolDescription,
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
            [Description("The path to the file to edit")]
            string filePath,
            [Description("The text to replace")] string oldString,
            [Description("The text to replace it with")]
            string newString,
            [Description("Replace all occurrences of oldString")]
            bool replaceAll,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            filePath = await _access.ResolvePathAsync(filePath, cwdPath, cancellationToken).ConfigureAwait(false);

            if (oldString == newString)
                return "No changes to make: oldString and newString are exactly the same.";

            using (await _fileLocks.AcquireAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                string? validationError = await ValidateAsync(filePath, _access, cancellationToken).ConfigureAwait(false);

                if (validationError is not null)
                    return validationError;

                try
                {
                    EditResult result = await _access.EditAsync(filePath, oldString, newString, replaceAll, cancellationToken).ConfigureAwait(false);
                    await FileToolGuards.UpdateReadStateAsync(filePath, _access, cancellationToken);

                    return replaceAll
                        ? $"The file '{filePath}' has been updated. All occurrences ({result.Occurrences}) were successfully replaced."
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

        public static async ValueTask<string?> ValidateAsync(
            string filePath, IFileSystemAccess access, CancellationToken cancellationToken) =>
            await FileToolGuards.ValidateReadStateAsync(filePath, access, cancellationToken);
    }
}
