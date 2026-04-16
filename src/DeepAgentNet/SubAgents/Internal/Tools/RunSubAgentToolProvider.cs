using DeepAgentNet.Agents.Internal;
using DeepAgentNet.Agents.Internal.Contracts;
using DeepAgentNet.ChatHistories.Internal;
using DeepAgentNet.Compactions.Internal;
using DeepAgentNet.FileSystems.Internal;
using DeepAgentNet.Shared.Contracts;
using DeepAgentNet.Shells.Internal;
using DeepAgentNet.TodoLists.Internal;
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
                Name = SubAgentDefaults.TaskToolName,
                Description = description?.Invoke(subAgents) ?? SubAgentDefaults.GetTaskToolDescription(subAgents),
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
                "subAgentType" => $"The name of the agent to use. Available: {string.Join(", ", subAgents.Select(a => $"'{a.Name}'"))}",
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
            [Description("Set only to resume a previous task. " +
                "Pass the task_id from a prior result to continue that subagent session instead of creating a fresh one.")]
            string? taskId = null,
            CancellationToken cancellationToken = default)
        {
            ResolvedSubAgent? resolvedSubAgent = await TryResolveSubAgentAsync(taskId, subAgentType, cancellationToken).ConfigureAwait(false);

            if (!resolvedSubAgent.HasValue)
                return $"Error: invoked agent of type {subAgentType}, the only allowed types are {string.Join(", ", _subAgentMap.Keys.Select(k => $"'{k}'"))}";

            (SubAgent subAgent, AIAgent agent, AgentSession session, string resolvedTaskId, bool resumed) = resolvedSubAgent.Value;

            await subAgent.Handle.OnSessionCreateOrResumedAsync(agent.Id, resolvedTaskId, resumed, description, prompt, cancellationToken)
                .ConfigureAwait(false);

            SubAgentRunner runner = new(resolvedSubAgent.Value);
            AgentResponse response = await runner.RunAsync(prompt, cancellationToken).ConfigureAwait(false);

            await subAgent.Handle.OnSessionCompletedAsync(agent.Id, resolvedTaskId, cancellationToken).ConfigureAwait(false);
            await SaveSessionEntryAsync(agent, session, resolvedTaskId, subAgent.Name, cancellationToken).ConfigureAwait(false);

            string result = response.Messages.Last().Text;

            return $"""
                task_id: {resolvedTaskId} (for resuming to continue this task if needed)

                <task_result>
                {result}
                </task_result>
                """;
        }

        private async ValueTask<ResolvedSubAgent?> TryResolveSubAgentAsync(
            string? taskId, string subAgentType, CancellationToken cancellationToken)
        {
            if (taskId is not null && TryGetSessionEntry(taskId) is { } entry &&
                _subAgentMap.TryGetValue(entry.SubAgentType, out var subAgent))
            {
                AIAgent agent = CreateAgentFromFactory(subAgent);

                AgentSession session = await agent.DeserializeSessionAsync(entry.SerializedState, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return new(SubAgent: subAgent, Agent: agent, Session: session, TaskId: taskId, Resumed: true);
            }

            if (_subAgentMap.TryGetValue(subAgentType, out subAgent))
            {
                AIAgent agent = CreateAgentFromFactory(subAgent);

                AgentSession session = await agent.CreateSessionAsync(cancellationToken)
                    .ConfigureAwait(false);

                taskId = Guid.NewGuid().ToString("N");
                return new(SubAgent: subAgent, Agent: agent, Session: session, TaskId: taskId, Resumed: false);
            }

            return null;
        }

        private AIAgent CreateAgentFromFactory(SubAgent subAgent)
        {
            IList<AIContextProvider> contextProviders = subAgent.Options is { } options
                ? ResolveContextProviders(options)
                : _defaultOptions.DefaultContextProviders;

            ChatClientAgentOptions agentOptions = subAgent.Factory.ProvideAgentOptions(
                _defaultOptions.DefaultOptions, contextProviders);

            IChatClient chatClient = subAgent.Factory.CreateChatClient(agentOptions.ChatOptions ?? new ChatOptions())
                ?? _defaultOptions.DefaultChatClient;

            chatClient = BuildChatClient(chatClient, subAgent);

            if (_defaultOptions.DeepAgentOptions.Compaction is not null ||
                subAgent.Options?.Compaction is not null)
            {
                agentOptions.ChatHistoryProvider = new NoOpChatHistoryProvider();
                agentOptions.ThrowOnChatHistoryProviderConflict = false;
            }

            agentOptions.UseProvidedChatClientAsIs = true;

            AIAgent agent = new ChatClientAgent(chatClient, agentOptions, _loggerFactory, _services);
            agent = agent.AsDeepAgent();

            return subAgent.Factory.DecorateAgent(agent);
        }

        private IChatClient BuildChatClient(IChatClient client, SubAgent subAgent)
        {
            ChatClientBuilder builder = client.AsBuilder();

            SubAgentOptions? options = subAgent.Options;

            builder = builder.Use(inner => inner.AsFunctionInvokingChatClient(
                options?.FunctionInvocation ?? _defaultOptions.DeepAgentOptions.FunctionInvocation,
                _loggerFactory, _services));

            if (options is not null)
            {
                IFunctionCallPreValidValidator validator = CreateFunctionCallPreValidValidator(options);
                builder.Use(inner => inner.AsFunctionCallPreValidatingChatClient(validator));

                if (options.TodoList is not null)
                    builder.Use(inner => inner.AsTodoListChatClient(options.TodoList));

                if (options.Compaction is not null)
                {
                    builder.UseCompactableChatHistory(options.Compaction);
                }
            }
            else
            {
                if (_parentAgent?.GetService<FunctionCallPreValidatingChatClient>() is { } preValidatingClient)
                    builder.Use(inner => inner.AsFunctionCallPreValidatingChatClient(preValidatingClient.FunctionCallPreValidator));

                if (_defaultOptions.DeepAgentOptions.TodoList is not null)
                    builder.Use(inner => inner.AsTodoListChatClient(_defaultOptions.DeepAgentOptions.TodoList));

                if (_defaultOptions.DeepAgentOptions.Compaction is not null)
                {
                    builder.UseCompactableChatHistory(_defaultOptions.DeepAgentOptions.Compaction);
                }
            }

            builder = builder.Use(inner => inner.AsCallIdSetterChatClient());
            return subAgent.Factory.DecorateChatClient(builder.Build());
        }

        private static IList<AIContextProvider> ResolveContextProviders(SubAgentOptions options)
        {
            List<AIContextProvider> providers = [];

            if (options.TodoList is not null)
                providers.Add(new TodoListProvider(options.TodoList));

            if (options.FileSystem is not null)
                providers.Add(new FileSystemProvider(options.FileSystem));

            if (options.Shell is not null)
                providers.Add(new ShellProvider(options.Shell));

            return providers;
        }

        private static IFunctionCallPreValidValidator CreateFunctionCallPreValidValidator(SubAgentOptions options)
        {
            FunctionCallPreValidValidator validator = new();

            if (options.FileSystem is not null)
                new FileSystemPreValidator(options.FileSystem.Access).Register(validator);

            return validator;
        }

        private SubAgentSessionEntry? TryGetSessionEntry(string taskId)
        {
            if (_parentSession?.StateBag is not { } stateBag)
                return null;

            string key = GetSubAgentSessionKey(taskId);
            return stateBag.GetValue<SubAgentSessionEntry>(key);
        }

        private async ValueTask SaveSessionEntryAsync(
            AIAgent agent, AgentSession session, string taskId, string subAgentType, CancellationToken cancellationToken)
        {
            if (_parentSession?.StateBag is not { } stateBag)
                return;

            JsonElement serializedState = await agent.SerializeSessionAsync(
                session, cancellationToken: cancellationToken).ConfigureAwait(false);

            string key = GetSubAgentSessionKey(taskId);
            stateBag.SetValue(key, new SubAgentSessionEntry(subAgentType, serializedState));
        }

        private class SubAgentRunner
        {
            private readonly SubAgent _subAgent;
            private readonly AIAgent _agent;
            private readonly AgentSession _session;

            public SubAgentRunner(ResolvedSubAgent subAgent)
            {
                _subAgent = subAgent.SubAgent;
                _agent = subAgent.Agent;
                _session = subAgent.Session;
            }

            public async ValueTask<AgentResponse> RunAsync(string prompt, CancellationToken cancellationToken)
            {
                AgentResponse response;
                List<AgentResponseUpdate> updates = new();

                List<ChatMessage> inputs = [new(ChatRole.User, prompt)];

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await foreach (AgentResponseUpdate update in
                        _agent.RunStreamingAsync(inputs, _session, cancellationToken: cancellationToken).ConfigureAwait(false))
                    {
                        await _subAgent.Handle.ReceiveUpdateAsync(_agent.Id, update, cancellationToken).ConfigureAwait(false);
                        updates.Add(update);
                    }

                    response = updates.ToAgentResponse();
                    await _subAgent.Handle.ReceiveResponseAsync(_agent.Id, response, cancellationToken).ConfigureAwait(false);

                    List<Task<ToolApprovalResponseContent>> approvalResultTasks = response.Messages.SelectMany(m => m.Contents)
                        .OfType<ToolApprovalRequestContent>()
                        .Select(c => _subAgent.Handle.ApproveToolCallAsync(_agent.Id, c, cancellationToken))
                        .ToList();

                    List<Task<FunctionResultContent>> callResultTasks = response.Messages.SelectMany(m => m.Contents)
                        .OfType<FunctionCallContent>()
                        .Where(c => !c.InformationalOnly)
                        .Select(async c => new FunctionResultContent(
                            c.CallId, await _subAgent.Handle.ProvideFunctionResultAsync(_agent.Id, c, cancellationToken).ConfigureAwait(false)
                        ))
                        .ToList();

                    if (!approvalResultTasks.Any() && !callResultTasks.Any())
                        break;

                    inputs.Clear();
                    updates.Clear();

                    IList<AIContent> approvalResults = await Task.WhenAll(approvalResultTasks).ConfigureAwait(false);
                    IList<AIContent> callResults = await Task.WhenAll(callResultTasks).ConfigureAwait(false);

                    inputs.Add(new ChatMessage(ChatRole.Tool, approvalResults.Concat(callResults).ToList())
                    {
                        MessageId = $"subagent:{Guid.NewGuid():N}"
                    });
                }

                return response;
            }
        }

        private static string GetSubAgentSessionKey(string taskId) => StateBagKeyPrefix + taskId;
        private record struct ResolvedSubAgent(SubAgent SubAgent, AIAgent Agent, AgentSession Session, string TaskId, bool Resumed);
    }
}
