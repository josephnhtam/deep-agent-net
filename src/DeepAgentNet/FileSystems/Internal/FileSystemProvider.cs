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

            ListInfoToolProvider listInfoToolProvider = new(options.Access, options.LsToolOptions);
            FileReadToolProvider fileReadToolProvider = new(options.Access, options.ReadFileToolOptions);
            FileWriteToolProvider fileWriteToolProvider = new(options.Access, options.WriteFileToolOptions);
            FileEditToolProvider fileEditToolProvider = new(options.Access, options.EditFileToolOptions);
            GlobToolProvider globToolProvider = new(options.Access, options.GlobToolOptions);
            GrepToolProvider grepToolProvider = new(options.Access, options.GrepToolOptions);

            _tools =
            [
                listInfoToolProvider.Tool,
                fileReadToolProvider.Tool,
                fileWriteToolProvider.Tool,
                fileEditToolProvider.Tool,
                globToolProvider.Tool,
                grepToolProvider.Tool
            ];
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            return new(new AIContext
            {
                Instructions = _options.SystemPrompt ?? FileSystemDefaults.SystemPrompt,
                Tools = _tools
            });
        }
    }
}
