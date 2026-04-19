using DeepAgentNet.FileSystems.Internal.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

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
            ILoggerFactory? loggerFactory = options.LoggerFactory;

            ListInfoToolProvider listInfoToolProvider = new(options.Access, options.LsToolOptions, loggerFactory);
            FileReadToolProvider fileReadToolProvider = new(options.Access, options.ReadFileToolOptions, loggerFactory);
            FileGetDataToolProvider fileGetDataToolProvider = new(options.Access, options.DataLimitedToolOptions, fileLocks, loggerFactory);
            FileWriteToolProvider fileWriteToolProvider = new(options.Access, options.WriteFileToolOptions, fileLocks, loggerFactory);
            FileOverwriteToolProvider fileOverwriteToolProvider = new(options.Access, options.OverwriteFileToolOptions, fileLocks, loggerFactory);
            FileEditToolProvider fileEditToolProvider = new(options.Access, options.EditFileToolOptions, fileLocks, loggerFactory);
            FileDeleteToolProvider fileDeleteToolProvider = new(options.Access, options.DeleteFileToolOptions, fileLocks, loggerFactory);
            GlobToolProvider globToolProvider = new(options.Access, options.GlobToolOptions, loggerFactory);
            GrepToolProvider grepToolProvider = new(options.Access, options.GrepToolOptions, loggerFactory);

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
            var envInfo = FileSystemEnvironmentInfo.Create(_options.Access.RootWorkingDirectory);

            return new(new AIContext
            {
                Instructions = _options.SystemPrompt?.Invoke(envInfo) ?? FileSystemDefaults.GetSystemPrompt(envInfo),
                Tools = _tools
            });
        }
    }
}
