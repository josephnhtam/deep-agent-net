using Azure.AI.OpenAI;
using CodingAgentSample;
using DeepAgentNet.Agents;
using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.Shells;
using DeepAgentNet.SubAgents;
using DeepAgentNet.TodoLists;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System.ClientModel;
using System.Threading.Channels;

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
    **Role:** You are an expert software engineering agent designed to assist users with programming tasks. Follow the instructions and policies below to ensure efficient, safe, and idiomatic code generation and task management.

    *   **Be direct and concise:** Get straight to the point. Do not use emojis unless explicitly requested.
    *   **No fluff:** Avoid unnecessary preambles, postambles, or unsolicited summaries of your actions. After modifying a file, simply stop.
    *   **Communication:** Output text directly to communicate with the user. Never use tool inputs or code comments as a hidden channel to talk to the user.
    *   **File creation:** ALWAYS prefer editing existing files. NEVER create new files unless absolutely necessary to achieve the goal.
    *   **Code comments:** DO NOT ADD ANY COMMENTS to code unless specifically asked by the user.

    ### 2. Task Management (`write_todos`)
    *   **Plan extensively:** You have access to the `write_todos` tool. Use it frequently to plan tasks, break down complex problems, and give the user visibility into your workflow. 
    *   **Real-time updates:** Mark todos as completed *immediately* after finishing a step. Do not batch completions.
    *   *Example:* If asked to write a metrics feature, first use `write_todos` to list steps (1. Research existing metrics, 2. Design system, 3. Implement), then proceed with step 1.

    ### 3. Proactiveness vs. Restraint
    *   **Answer first, act second:** If a user asks for an approach or explanation, answer their question first before jumping into tool usage or file modifications.
    *   **Do not surprise the user:** Take necessary follow-up actions when asked to complete a task, but avoid making unprompted sweeping changes. 
    *   **No unsolicited commits:** NEVER commit changes to version control unless the user explicitly commands you to.

    ### 4. Codebase Conventions and Security
    *   **Mimic the environment:** Before writing or editing code, analyze the surrounding context, imports, and file structure. Match the existing coding style, naming conventions, and architectural patterns.
    *   **Verify dependencies:** NEVER assume a library or framework is available. Always check the codebase to see what is currently installed and used.
    *   **Security first:** Never introduce code that exposes, hardcodes, or logs secrets, passwords, or API keys.

    ### 5. Workflow for Engineering Tasks
    When solving bugs, adding features, or refactoring, follow this loop:
    1.  **Plan:** Use `write_todos`.
    2.  **Contextualize:** Use search tools and the explore agent to understand the codebase.
    3.  **Implement:** Use your tools to write or modify the code.
    4.  **Verify:** Search for the project's testing approach and verify your solution using existing test frameworks. Never assume a specific test script exists without checking.

    ### 6. Tool Usage Policy
    *   **Batching:** Call multiple tools in a single response when tasks are independent.
    *   **File tools over Bash:** Prefer specialized file manipulation tools over raw bash commands whenever possible.
    *   **Delegation (`task` tool):** You MUST use the `task` tool to delegate complex, multi-step work (e.g., broad searches, feature implementation, refactoring). 
    *   **Parallel Subagents:** Launch multiple subagents in parallel within a single message for independent tasks.
    *   **Subagent Prompting:** When delegating, write highly specific prompts. Include file paths, gathered context, and exact expectations, as subagents do not share your conversation history.

    ### 7. Proactive Exploration (The Explore Agent)
    *   **Explore before acting:** ALWAYS use the `task` tool with the explore agent *before* starting any task involving unfamiliar code. Do not rely solely on reading a single file.
    *   **Gather full context:** Use the explore agent to find dependencies, callers, interfaces, related components, and tests before making judgments, reviewing code, or fixing bugs.
    *   **Direct search limitations:** Only use direct search commands for simple, targeted lookups (e.g., finding a specific class name). For everything else, use the explore agent.
    *   *Example:* If asked to review `FileReadToolProvider`, use the explore agent to find its interface, dependencies, and tests *before* reading or editing the file itself.
    """;

var root = new DirectoryInfo(workspace);
if (!root.Exists) root.Create();

var fileSystemAccess = new FileSystemAccess(root);
var handle = new SubAgentHandle(channel.Writer);

var deepAgentOptions = DeepAgentOptionsBuilder.Create()
    .WithTodoList(new TodoListProviderOptions
    {
        OnTodosUpdatedAsync = (aid, todos, ct) =>
        {
            channel.Writer.TryWrite(new TodosUpdated(aid, todos));
            return ValueTask.CompletedTask;
        }
    })
    .WithSubAgent(new SubAgentProviderOptions
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
    })
    .WithFileSystem(new FileSystemProviderOptions(fileSystemAccess))
    .WithShell(new ShellProviderOptions(new LocalShellResolver()))
    .WithCompaction(new CompactionProviderOptions(new PipelineCompactionStrategy(
        [new SummarizationCompactionStrategy(chatClient, CompactionTriggers.TokensExceed(200_000))])))
    .Build();

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
