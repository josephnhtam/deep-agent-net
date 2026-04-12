using DeepAgentNet.FileSystems.Internal.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.FileSystems.Internal
{
    internal class FileSystemProvider : AIContextProvider
    {
        private readonly FileSystemProviderOptions _options;
        private readonly List<AITool> _tools;

        public FileSystemProvider(FileSystemProviderOptions options)
        {
            _options = options;

            FileLocks fileLocks = new();

            ListInfoToolProvider listInfoToolProvider = new(options.Access, options.LsToolOptions);
            FileReadToolProvider fileReadToolProvider = new(options.Access, options.ReadFileToolOptions);
            FileGetDataToolProvider fileGetDataToolProvider = new(options.Access, options.DataLimitedToolOptions, fileLocks);
            FileWriteToolProvider fileWriteToolProvider = new(options.Access, options.WriteFileToolOptions, fileLocks);
            FileOverwriteToolProvider fileOverwriteToolProvider = new(options.Access, options.OverwriteFileToolOptions, fileLocks);
            FileEditToolProvider fileEditToolProvider = new(options.Access, options.EditFileToolOptions, fileLocks);
            FileDeleteToolProvider fileDeleteToolProvider = new(options.Access, options.DeleteFileToolOptions, fileLocks);
            GlobToolProvider globToolProvider = new(options.Access, options.GlobToolOptions);
            GrepToolProvider grepToolProvider = new(options.Access, options.GrepToolOptions);

            _tools =
            [
                listInfoToolProvider.Tool,
                fileReadToolProvider.Tool,
                fileGetDataToolProvider.Tool,
                fileWriteToolProvider.Tool,
                fileOverwriteToolProvider.Tool,
                fileEditToolProvider.Tool,
                fileDeleteToolProvider.Tool,
                globToolProvider.Tool,
                grepToolProvider.Tool
            ];
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            return new(new AIContext
            {
                Instructions = _options.SystemPrompt?.Invoke(_options.Access.RootWorkingDirectory) ??
                    FileSystemDefaults.GetSystemPrompt(_options.Access.RootWorkingDirectory),
                Tools = _tools
            });
        }
    }
}
