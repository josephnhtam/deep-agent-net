using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace DeepAgentNet.SubAgents.Internal
{
    internal class SubAgentProvider : AIContextProvider
    {
        private readonly SubAgentDefaultOptions _defaultOptions;
        private readonly SubAgentProviderOptions? _options;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly IServiceProvider? _services;

        private readonly List<AITool> _tools;
        private readonly Dictionary<string, SubAgent> _subAgentMap;

        public SubAgentProvider(SubAgentDefaultOptions defaultOptions, SubAgentProviderOptions? options = null,
            ILoggerFactory? loggerFactory = null, IServiceProvider? services = null)
        {
            _defaultOptions = defaultOptions;
            _options = options;
            _loggerFactory = loggerFactory;
            _services = services;
            _tools = new List<AITool>();

            List<SubAgent> subAgents = CreateSubAgents(options);

            if (subAgents.Any())
            {
                AIFunction tool = AIFunctionFactory.Create(ExecuteSubAgentAsync, new AIFunctionFactoryOptions
                {
                    Name = SubAgentDefaults.ToolName,
                    Description = options?.ToolDescription?.Invoke(subAgents) ?? SubAgentDefaults.GetToolDescription([]),
                    JsonSchemaCreateOptions = JsonSchemaCreateOptions(subAgents)
                });

                _tools.Add(tool);
            }

            _subAgentMap = subAgents.ToDictionary(a => a.Name, a => a);
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            if (!_tools.Any())
            {
                return new(new AIContext());
            }

            return new(new AIContext
            {
                Instructions = _options?.SystemPrompt ?? SubAgentDefaults.SystemPrompt,
                Tools = _tools
            });
        }

        private static AIJsonSchemaCreateOptions JsonSchemaCreateOptions(IList<SubAgent> subAgents) => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "subAgentType" => $"The name of the agent to use. Available: {string.Join(", ", subAgents.Select(a => a.Name))}",
                _ => null
            }
        };

        private List<SubAgent> CreateSubAgents(SubAgentProviderOptions? options = null)
        {
            List<SubAgent> subAgents = new();

            if (options?.GeneralPurposeAgent is not null)
            {
                ValueTask<AIAgent> GeneralPurposeAgentFactory(
                    SubAgentDefaultOptions defaultOptions,
                    ILoggerFactory? loggerFactory,
                    IServiceProvider? services,
                    CancellationToken cancellation)
                {
                    ChatClientAgentOptions agentOptions = defaultOptions.DefaultOptions.Clone();
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

            if (options?.SubAgents is { Count: > 0 })
            {
                subAgents.AddRange(options.SubAgents);
            }

            return subAgents;
        }

        private async ValueTask<string> ExecuteSubAgentAsync(
            [Description("The task to execute with the selected agent")]
            string description,
            string subAgentType,
            CancellationToken cancellationToken)
        {
            if (!_subAgentMap.TryGetValue(subAgentType, out SubAgent? subAgent))
            {
                return $"Error: invoked agent of type ${subAgentType}, the only allowed types are ${string.Join(", ", _subAgentMap.Keys)}";
            }

            AIAgent agent = await subAgent.Factory(_defaultOptions, _loggerFactory, _services, cancellationToken);
            AgentSession session = await agent.CreateSessionAsync(cancellationToken);
            List<ChatMessage> inputs = [new(ChatRole.User, description)];

            List<AgentResponseUpdate> updates = new();
            AgentResponse response;

            while (true)
            {
                await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(inputs, session, cancellationToken: cancellationToken))
                {
                    updates.Add(update);
                }

                response = updates.ToAgentResponse();

                List<Task<FunctionApprovalResponseContent>> approvalResultTasks = response.Messages.SelectMany(m => m.Contents)
                    .OfType<FunctionApprovalRequestContent>()
                    .Select(c => subAgent.Handle.ApproveFunctionCallAsync(c, cancellationToken))
                    .ToList();

                HashSet<string> completedCallIds = response.Messages.SelectMany(m => m.Contents)
                    .OfType<FunctionResultContent>()
                    .Select(c => c.CallId)
                    .ToHashSet();

                List<Task<FunctionResultContent>> callResultTasks = response.Messages.SelectMany(m => m.Contents)
                    .OfType<FunctionCallContent>()
                    .Where(c => !completedCallIds.Contains(c.CallId))
                    .Select(async c => new FunctionResultContent(c.CallId, await subAgent.Handle.ProvideFunctionResultAsync(c, cancellationToken)))
                    .ToList();

                if (!approvalResultTasks.Any() && !callResultTasks.Any())
                {
                    break;
                }

                inputs.Clear();
                updates.Clear();

                IList<AIContent> approvalResults = await Task.WhenAll(approvalResultTasks);
                IList<AIContent> callResults = await Task.WhenAll(callResultTasks);
                inputs.Add(new ChatMessage(ChatRole.Tool, approvalResults.Concat(callResults).ToList()));
            }

            return response.Messages.Last().Text;
        }
    }
}
