using DeepAgentNet.SubAgents.Contracts;
using DeepAgentNet.SubAgents.Internal.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DeepAgentNet.SubAgents.Internal
{
    internal class SubAgentProvider : AIContextProvider
    {
        private readonly SubAgentProviderOptions _options;
        private readonly List<AITool> _tools;
        private readonly RunSubAgentToolProvider? _runSubAgentToolProvider;

        public SubAgentProvider(SubAgentDefaultOptions defaultOptions, SubAgentProviderOptions? options,
            ILoggerFactory? loggerFactory = null, IServiceProvider? services = null)
        {
            _options = options ?? new SubAgentProviderOptions();
            _tools = new List<AITool>();

            List<SubAgent> subAgents = CreateSubAgents(_options);
            if (subAgents.Any())
            {
                _runSubAgentToolProvider = new(subAgents, defaultOptions, _options.ToolDescription, loggerFactory, services);
                _tools.Add(_runSubAgentToolProvider.Tool);
            }
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            if (!_tools.Any())
            {
                return new(new AIContext());
            }

            _runSubAgentToolProvider?.SetParentContext(context.Agent, context.Session);

            return new(new AIContext
            {
                Instructions = _options.SystemPrompt ?? SubAgentDefaults.SystemPrompt,
                Tools = _tools
            });
        }

        private List<SubAgent> CreateSubAgents(SubAgentProviderOptions options)
        {
            List<SubAgent> subAgents = new();

            if (options.GeneralPurposeAgent is not null)
            {
                ISubAgentFactory factory = options.GeneralPurposeAgent.Factory ??
                    new DefaultSubAgentFactory(options.GeneralPurposeAgent.SystemPrompt ??
                        SubAgentDefaults.GeneralPurposeAgentSystemPrompt);

                subAgents.Add(new SubAgent(
                    Name: SubAgentDefaults.GeneralPurposeAgentName,
                    Description: options.GeneralPurposeAgent.Description ?? SubAgentDefaults.GeneralPurposeAgentDescription,
                    Handle: options.GeneralPurposeAgent.Handle,
                    Factory: factory
                )
                { Options = options.GeneralPurposeAgent.Options });
            }

            if (options.SubAgents is { Count: > 0 })
            {
                subAgents.AddRange(options.SubAgents);
            }

            return subAgents;
        }
    }
}
