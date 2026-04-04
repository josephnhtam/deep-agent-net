using DeepAgentNet.Shared.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace DeepAgentNet.SubAgents.Internal.Tools
{
    internal class RunSubAgentToolProvider : IToolProvider
    {
        private readonly Dictionary<string, SubAgent> _subAgentMap;
        private readonly SubAgentDefaultOptions _defaultOptions;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly IServiceProvider? _services;

        public AITool Tool { get; }

        public RunSubAgentToolProvider(List<SubAgent> subAgents, SubAgentDefaultOptions defaultOptions,
            Func<IList<SubAgent>, string>? description, ILoggerFactory? loggerFactory, IServiceProvider? services)
        {
            _defaultOptions = defaultOptions;
            _loggerFactory = loggerFactory;
            _services = services;
            _subAgentMap = subAgents.ToDictionary(a => a.Name, a => a);

            Tool = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = SubAgentDefaults.ToolName,
                Description = description?.Invoke(subAgents) ?? SubAgentDefaults.GetToolDescription(subAgents),
                JsonSchemaCreateOptions = JsonSchemaCreateOptions(subAgents)
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

        private async ValueTask<string> ExecuteAsync(
            [Description("The task to execute with the selected agent")]
            string description,
            string subAgentType,
            CancellationToken cancellationToken)
        {
            if (!_subAgentMap.TryGetValue(subAgentType, out SubAgent? subAgent))
            {
                return $"Error: invoked agent of type {subAgentType}, the only allowed types are {string.Join(", ", _subAgentMap.Keys)}";
            }

            AIAgent agent = await subAgent.Factory(_defaultOptions, _loggerFactory, _services, cancellationToken).ConfigureAwait(false);
            AgentSession session = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
            List<ChatMessage> inputs = [new(ChatRole.User, description)];

            List<AgentResponseUpdate> updates = new();
            AgentResponse response;

            while (true)
            {
                await foreach (AgentResponseUpdate update in
                    agent.RunStreamingAsync(inputs, session, cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    await subAgent.Handle.ReceiveUpdateAsync(agent.Id, update, cancellationToken).ConfigureAwait(false);
                    updates.Add(update);
                }

                response = updates.ToAgentResponse();
                await subAgent.Handle.ReceiveResponseAsync(agent.Id, response, cancellationToken).ConfigureAwait(false);

                List<Task<FunctionApprovalResponseContent>> approvalResultTasks = response.Messages.SelectMany(m => m.Contents)
                    .OfType<FunctionApprovalRequestContent>()
                    .Select(c => subAgent.Handle.ApproveFunctionCallAsync(agent.Id, c, cancellationToken))
                    .ToList();

                HashSet<string> completedCallIds = response.Messages.SelectMany(m => m.Contents)
                    .OfType<FunctionResultContent>()
                    .Select(c => c.CallId)
                    .ToHashSet();

                List<Task<FunctionResultContent>> callResultTasks = response.Messages.SelectMany(m => m.Contents)
                    .OfType<FunctionCallContent>()
                    .Where(c => !completedCallIds.Contains(c.CallId))
                    .Select(async c => new FunctionResultContent(
                        c.CallId, await subAgent.Handle.ProvideFunctionResultAsync(agent.Id, c, cancellationToken).ConfigureAwait(false)
                    ))
                    .ToList();

                if (!approvalResultTasks.Any() && !callResultTasks.Any())
                    break;

                inputs.Clear();
                updates.Clear();

                IList<AIContent> approvalResults = await Task.WhenAll(approvalResultTasks).ConfigureAwait(false);
                IList<AIContent> callResults = await Task.WhenAll(callResultTasks).ConfigureAwait(false);
                inputs.Add(new ChatMessage(ChatRole.Tool, approvalResults.Concat(callResults).ToList()));
            }

            return response.Messages.Last().Text;
        }
    }
}
