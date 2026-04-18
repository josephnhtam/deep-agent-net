using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class FileGetDataToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly DataLimitedToolOptions _options;
        private readonly IFileLocks _fileLocks;

        public AITool Tool { get; }

        internal FileGetDataToolProvider(IFileSystemAccess access, DataLimitedToolOptions options, IFileLocks fileLocks)
        {
            _access = access;
            _options = options;
            _fileLocks = fileLocks;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.ReadFileDataToolName,
                Description = options.Description ?? FileSystemDefaults.ReadFileDataToolDescription,
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

        private async ValueTask<IEnumerable<AIContent>> ExecuteAsync(
            [Description("The path to the file to read as raw bytes")]
            string filePath,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            filePath = PathHelper.NormalizePath(filePath);

            if (!Path.IsPathFullyQualified(filePath))
                filePath = Path.Combine(cwdPath ?? _access.RootWorkingDirectory, filePath);

            using (await _fileLocks.AcquireAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await using Stream stream = await _access.ReadDataAsync(filePath, cancellationToken).ConfigureAwait(false);

                    long byteLength = stream.CanSeek ? stream.Length : 0;

                    if (byteLength > _options.ResultBytesLimit)
                    {
                        return [new TextContent($"Error: file exceeds maximum size of {_options.ResultBytesLimit} bytes.")];
                    }

                    string? mediaType = GuessMediaType(filePath);
                    DataContent dataContent = await DataContent.LoadFromAsync(stream, mediaType, cancellationToken).ConfigureAwait(false);

                    await FileToolGuards.RecordFileReadAsync(filePath, _access, cancellationToken).ConfigureAwait(false);

                    return
                    [
                        new TextContent($"The file `{filePath}` is attached below as binary data ({byteLength} bytes)."),
                        dataContent
                    ];
                }
                catch (Exception ex)
                {
                    return [new TextContent($"Error reading `{filePath}`: {ex.Message}")];
                }
            }
        }

        private static string? GuessMediaType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".txt" or ".md" or ".cs" or ".jsonc" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }
}
