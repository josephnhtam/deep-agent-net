using DeepAgentNet.Shells.Contracts;
using DeepAgentNet.Shells.Internal.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Shells.Internal
{
    internal class ShellProvider : AIContextProvider
    {
        private readonly ShellProviderOptions _options;
        private readonly List<AITool> _tools;

        public ShellProvider(ShellProviderOptions options)
        {
            _options = options;

            List<IShellRunner> shells = _options.ShellResolver.ResolveShells();
            ShellToolProvider shellToolProvider = new(shells, _options, _options.LoggerFactory);

            _tools = [shellToolProvider.Tool];
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(
            InvokingContext context, CancellationToken cancellationToken = default)
        {
            return new(new AIContext
            {
                Instructions = _options.SystemPrompt ?? ShellDefaults.SystemPrompt,
                Tools = _tools,
            });
        }
    }
}
