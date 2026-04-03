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

        public SubAgentProvider(SubAgentDefaultOptions defaultOptions, SubAgentProviderOptions? options = null,
            ILoggerFactory? loggerFactory = null, IServiceProvider? services = null)
        {
            _options = options ?? new SubAgentProviderOptions();
            _tools = new List<AITool>();

            List<SubAgent> subAgents = CreateSubAgents(_options);
            if (subAgents.Any())
            {
                RunSubAgentToolProvider provider = new(subAgents, defaultOptions, _options.ToolDescription, loggerFactory, services);
                _tools.Add(provider.Tool);
            }
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            if (!_tools.Any())
            {
                return new(new AIContext());
            }

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
                ValueTask<AIAgent> GeneralPurposeAgentFactory(
                    SubAgentDefaultOptions defaultOptions,
                    ILoggerFactory? loggerFactory,
                    IServiceProvider? services,
                    CancellationToken cancellation)
                {
                    ChatClientAgentOptions agentOptions = defaultOptions.DefaultOptions.Clone();

                    agentOptions.ChatOptions = agentOptions.ChatOptions?.Clone() ?? new ChatOptions();
                    agentOptions.ChatOptions.Instructions = options.GeneralPurposeAgent.SystemPrompt ?? SubAgentDefaults.GeneralPurposeAgentSystemPrompt;

                    agentOptions.AIContextProviders =
                    [
                        ..agentOptions.AIContextProviders ?? [],
                        ..defaultOptions.DefaultGeneralPurposeContextProviders
                    ];

                    return new(new ChatClientAgent(defaultOptions.DefaultChatClient, agentOptions, loggerFactory, services));
                }

                subAgents.Add(new SubAgent(
                    Name: SubAgentDefaults.GeneralPurposeAgentName,
                    Description: options.GeneralPurposeAgent.Description ?? SubAgentDefaults.GeneralPurposeAgentDescription,
                    Handle: options.GeneralPurposeAgent.Handle,
                    Factory: options.GeneralPurposeAgent.Factory ?? GeneralPurposeAgentFactory
                ));
            }

            if (options.SubAgents is { Count: > 0 })
            {
                subAgents.AddRange(options.SubAgents);
            }

            return subAgents;
        }
    }
}
