using DeepAgentNet.Shared.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;

namespace DeepAgentNet.SubAgents.Internal.Tools
{
    internal record SubAgentSessionEntry(string SubAgentType, JsonElement SerializedState);

    internal class RunSubAgentToolProvider : IToolProvider
    {
        private const string StateBagKeyPrefix = "SubAgent:";

        private readonly Dictionary<string, SubAgent> _subAgentMap;
        private readonly SubAgentDefaultOptions _defaultOptions;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly IServiceProvider? _services;

        private AIAgent? _parentAgent;
        private AgentSession? _parentSession;

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

        internal void SetParentContext(AIAgent agent, AgentSession? session)
        {
            _parentAgent = agent;
            _parentSession = session;
        }

        private static AIJsonSchemaCreateOptions JsonSchemaCreateOptions(IList<SubAgent> subAgents) => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "subAgentType" => $"The name of the agent to use. Available: {string.Join(", ", subAgents.Select(a => a.Name))}",
                "taskId" => "Set only to resume a previous task. Pass the task_id from a prior result to continue that subagent session.",
                _ => null
            }
        };

        private async ValueTask<string> ExecuteAsync(
            [Description("A short (3-5 words) description of the task")]
            string description,
            [Description("The detailed description and expected result of the task for the agent to perform")]
            string prompt,
            string subAgentType,
            [Description("Set only to resume a previous task. Pass the task_id from a prior result to continue that subagent session instead of creating a fresh one.")]
            string? taskId = null,
            CancellationToken cancellationToken = default)
        {
            string resolvedTaskId;
            AIAgent agent;
            AgentSession session;
            SubAgent subAgent;
            bool resumed = false;

            if (taskId is not null && TryGetSessionEntry(taskId, out SubAgentSessionEntry? entry) &&
                _subAgentMap.TryGetValue(entry!.SubAgentType, out subAgent!))
            {
                agent = await subAgent.Factory(_defaultOptions, _loggerFactory, _services, cancellationToken).ConfigureAwait(false);
                session = await agent.DeserializeSessionAsync(entry.SerializedState, cancellationToken: cancellationToken).ConfigureAwait(false);
                resolvedTaskId = taskId;
                resumed = true;
            }
            else if (_subAgentMap.TryGetValue(subAgentType, out subAgent!))
            {
                agent = await subAgent.Factory(_defaultOptions, _loggerFactory, _services, cancellationToken).ConfigureAwait(false);
                session = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
                resolvedTaskId = Guid.NewGuid().ToString("N");
            }
            else
            {
                return $"Error: invoked agent of type {subAgentType}, the only allowed types are {string.Join(", ", _subAgentMap.Keys)}";
            }

            await subAgent.Handle.OnSessionCreatedAsync(agent.Id, resolvedTaskId, resumed, cancellationToken).ConfigureAwait(false);

            List<ChatMessage> inputs = [new(ChatRole.User, prompt)];

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

                List<Task<ToolApprovalResponseContent>> approvalResultTasks = response.Messages.SelectMany(m => m.Contents)
                    .OfType<ToolApprovalRequestContent>()
                    .Select(c => subAgent.Handle.ApproveToolCallAsync(agent.Id, c, cancellationToken))
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

            await SaveSessionEntryAsync(agent, session, resolvedTaskId, subAgent.Name, cancellationToken).ConfigureAwait(false);

            string result = response.Messages.Last().Text;
            return $"""
                task_id: {resolvedTaskId} (for resuming to continue this task if needed)

                <task_result>
                {result}
                </task_result>
                """;
        }

        private bool TryGetSessionEntry(string taskId, out SubAgentSessionEntry? entry)
        {
            entry = null;

            if (_parentSession?.StateBag is not { } stateBag)
                return false;

            string key = StateBagKeyPrefix + taskId;
            entry = stateBag.GetValue<SubAgentSessionEntry>(key);
            return entry is not null;
        }

        private async ValueTask SaveSessionEntryAsync(
            AIAgent agent, AgentSession session, string taskId, string subAgentType, CancellationToken cancellationToken)
        {
            if (_parentSession?.StateBag is not { } stateBag)
                return;

            JsonElement serializedState = await agent.SerializeSessionAsync(
                session, cancellationToken: cancellationToken).ConfigureAwait(false);

            string key = StateBagKeyPrefix + taskId;
            stateBag.SetValue(key, new SubAgentSessionEntry(subAgentType, serializedState));
        }
    }
}
