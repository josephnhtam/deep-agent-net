using System.Threading.Channels;
using Azure.AI.OpenAI;
using CodingAgentSample;
using DeepAgentNet.Agents;
using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.SubAgents;
using DeepAgentNet.TodoLists;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System.ClientModel;

const string workspace = "./workspace";

var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_API_KEY environment variable.");
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT environment variable.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient();

var agentId = Guid.NewGuid().ToString();
var channel = Channel.CreateUnbounded<AgentEvent>();
var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var instruction = """
    You are a software engineering agent. You help users with programming tasks including writing code, debugging, refactoring, and explaining code.

    Guidelines:
    - Be concise and direct.
    - Use the write_todos tool to plan and track multi-step tasks.
    - Mark todos as completed immediately after finishing each one.
    - Use the task tool to delegate complex, multi-step work to sub-agents.
    - Follow existing code conventions and patterns.
    - Do not add unnecessary comments to code.
    - You have the capability to call multiple tools in a single response. Batch independent tool calls for optimal performance.
    """;

var root = new DirectoryInfo(workspace);
if (!root.Exists) root.Create();

var fileSystemAccess = new FileSystemAccess(root);
var handle = new SubAgentHandle(channel.Writer);

var deepAgentOptions = new DeepAgentOptions
{
    TodoList = new TodoListProviderOptions
    {
        OnTodosUpdatedAsync = (aid, todos, ct) =>
        {
            channel.Writer.TryWrite(new TodosUpdated(aid, todos));
            return ValueTask.CompletedTask;
        }
    },
    SubAgent = new SubAgentProviderOptions
    {
        GeneralPurposeAgent = new GeneralPurposeAgentOptions(handle)
        {
            Description = "General-purpose agent for executing multi-step tasks in parallel.",
            SystemPrompt = """
                You are a software engineering agent completing a delegated task.
                Focus on completing the specific task. Be thorough and use search tools before making changes.
                Follow existing code conventions. Do not add unnecessary comments.
                """
        },
        SubAgents =
        [
            new SubAgent(
                Name: "explore",
                Description: """
                Codebase exploration agent.
                Use for finding files, searching code, summarizing, and answering questions about the codebase.
                """,
                Handle: handle,
                Factory: new ExploreSubAgentFactory())
        ]
    },
    FileSystem = new FileSystemProviderOptions(fileSystemAccess),
    Compaction = new CompactionProviderOptions(new PipelineCompactionStrategy(
        [new SummarizationCompactionStrategy(chatClient, CompactionTriggers.TokensExceed(200_000))]))
};

var agent = chatClient.AsDeepAgent(
    agentOptions: new ChatClientAgentOptions
    {
        Id = agentId,
        ChatOptions = new()
        {
            Instructions = instruction,
            Reasoning = new ReasoningOptions
            {
                Output = ReasoningOutput.Full,
                Effort = ReasoningEffort.Low
            }
        }
    },
    deepAgentOptions: deepAgentOptions);

var session = await agent.CreateSessionAsync();

var agentTurnRunner = new AgentTurnRunner(agent, session, channel.Writer);

var console = new CodingAgentConsole(agentTurnRunner, channel.Reader);

await console.RunAsync(cts.Token);
