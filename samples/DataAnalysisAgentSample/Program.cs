using DeepAgentNet.Agents;
using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.Shells;
using DeepAgentNet.SubAgents;
using DeepAgentNet.TodoLists;
using DeepAgentNet.Tools.SqlDatabaseTools;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Adapters;
using DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Adapters;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using SampleUtilities.AgentConsoles;
using SampleUtilities.ChatClients;

const string workspace = "./workspace";

IChatClientProvider chatClientProvider = args switch
{
    _ when args.Contains("openai") => new OpenAIChatClientProvider(),
    _ when args.Contains("vertexai") => new VertexAIChatClientProvider(),
    _ => new AzureOpenAIChatClientProvider(),
};

var chatClient = chatClientProvider.GetChatClient();
var agentId = Guid.NewGuid().ToString();
var cts = new CancellationTokenSource();

var consoleBuilder = AgentConsoleBuilder.Create();
var channelWriter = consoleBuilder.ChannelWriter;
var handle = consoleBuilder.SubAgentHandle;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var instruction = """
    You are an expert data analysis agent with access to a SQL database, a file system, and a shell for running code and plotting graph.

    ### Workflow
    1. **Plan first.** When you receive a request, immediately use `write_todos` to break it into concrete steps (e.g. explore schema, write queries, analyze results, visualize). Update todo status in real time as you complete each step.
    2. **Explore the database.** Inspect tables, columns, and relationships before writing queries. Understand the data before drawing conclusions.
    3. **Query the database.** Write precise SQL to extract the data you need. Prefer CTEs and window functions for clarity.
    4. **Analyze with Python.** When deeper statistical analysis, data transformation, or visualization is needed, write a Python script, and execute it via the shell.
       - Use `pandas` for data manipulation and `matplotlib` or `seaborn` for charts.
       - Tell the user where to find the image files (PNG).
       - Tell the user the key findings.
    5. **Summarize.** After completing the analysis, provide a clear, concise summary of the findings with specific numbers and takeaways.

    ### Guidelines
    - Be direct and concise. No unnecessary preamble.
    - When the user's request is ambiguous, outline your interpretation and assumptions before proceeding.
    - Prefer SQL for simple aggregations; switch to Python when you need statistical tests, or visualizations.
    - Never try to access the database directly.
    """;

var root = new DirectoryInfo(workspace);
if (!root.Exists) root.Create();

var fileSystemAccess = new FileSystemAccess(root);

var deepAgentOptions = DeepAgentOptionsBuilder.Create()
    .WithTodoList(new TodoListProviderOptions
    {
        OnTodosUpdatedAsync = (aid, todos, ct) =>
        {
            channelWriter.TryWrite(new TodosUpdated(aid, todos));
            return ValueTask.CompletedTask;
        }
    })
    .WithSubAgent(new SubAgentProviderOptions
    {
        GeneralPurposeAgent = new GeneralPurposeAgentOptions(handle)
    })
    .WithFileSystem(new FileSystemProviderOptions(fileSystemAccess))
    .WithShell(new ShellProviderOptions(new LocalShellResolver())
    {
        DefaultWorkingDirectory = root.FullName
    })
    .WithCompaction(new CompactionProviderOptions(new PipelineCompactionStrategy(
        [new SummarizationCompactionStrategy(chatClient, CompactionTriggers.TokensExceed(200_000))])))
    .Build();

var connectionFactory = () => new SqliteConnection("Data Source=Chinook_Sqlite.sqlite;Mode=ReadOnly");

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
        },
        AIContextProviders =
        [
            new SqlDatabaseContextProvider(new SqlContextProviderOptions(
                Inspector: new SqliteInspector(connectionFactory),
                Executor: new SqliteExecutor(connectionFactory)
            ) { IsReadOnly = true, FileSystemAccess = fileSystemAccess })
        ]
    },
    deepAgentOptions: deepAgentOptions);

var session = await agent.CreateSessionAsync();

var console = consoleBuilder.Build("DeepAgentNet Data Analysis Agent", agent, session);
await console.RunAsync(cts.Token);
