# DeepAgentNet

### The agent harness for .NET.

[![NuGet](https://img.shields.io/nuget/v/DeepAgentNet.svg)](https://www.nuget.org/packages/DeepAgentNet)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

DeepAgentNet is an agent harness for .NET, a ready-to-run framework for building autonomous agents. Instead of wiring up prompts, tools, and context management yourself, you get a working agent out of the box and customize what you need.

Built on [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp) and [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions).

**What's included:**

- **Planning**: `write_todos` for task breakdown and progress tracking
- **Filesystem**: `read_file`, `write_file`, `edit_file`, `delete_file`, `ls`, `glob`, `grep` for sandboxed file access
- **Shell**: `shell` for running commands with cross-platform shell detection
- **Sub-agents**: `task` for delegating work with isolated context windows and session resume
- **Context management**: chat history and compaction integrated at the chat client level for automatic context management during autonomous function calls
- **Tool approval**: human-in-the-loop gates for sensitive operations

## Installation

```
dotnet add package DeepAgentNet
```

## Quick Start

OpenAI, Ollama, and other providers work too; see [`samples/SampleUtilities/ChatClients`](samples/SampleUtilities/ChatClients) for examples.

```csharp
using Azure.AI.OpenAI;
using DeepAgentNet.Agents;
using DeepAgentNet.FileSystems;
using DeepAgentNet.Shells;
using DeepAgentNet.SubAgents;
using DeepAgentNet.SubAgents.Contracts;
using DeepAgentNet.TodoLists;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ClientModel;

var handle = new AutoApproveSubAgentHandle();
var chatClient = new AzureOpenAIClient(
    new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!),
    new ApiKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!))
    .GetChatClient(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini")
    .AsIChatClient();

var workspace = new DirectoryInfo("./workspace");
if (!workspace.Exists) workspace.Create();

var options = DeepAgentOptionsBuilder.Create()
    .WithTodoList()
    .WithFileSystem(new FileSystemProviderOptions(new FileSystemAccess(workspace)))
    .WithShell(new ShellProviderOptions(new LocalShellResolver())
    {
        DefaultWorkingDirectory = workspace.FullName
    })
    .WithSubAgent(new SubAgentProviderOptions
    {
        GeneralPurposeAgent = new GeneralPurposeAgentOptions(handle)
    })
    .Build();

var agent = chatClient.AsDeepAgent(
    agentOptions: new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions
        {
            Instructions = "You are a helpful autonomous agent."
        }
    },
    deepAgentOptions: options);

var session = await agent.CreateSessionAsync();
var inputs = new List<ChatMessage> { new(ChatRole.User, "Hello!") };

while (true)
{
    var updates = new List<AgentResponseUpdate>();
    await foreach (var update in agent.RunStreamingAsync(inputs, session))
    {
        Console.Write(update.Text);
        updates.Add(update);
    }

    var approvals = updates.ToAgentResponse().Messages
        .SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
    if (approvals.Count > 0)
    {
        var results = new List<AIContent>();
        foreach (var approval in approvals)
        {
            Console.Write($"\nApprove {approval.ToolCall}? (y/n): ");
            results.Add(approval.CreateResponse(Console.ReadLine()?.Trim().ToLower() == "y"));
        }
        inputs = [new ChatMessage(ChatRole.Tool, results)];
        continue;
    }

    Console.Write("\nYou: ");
    var userInput = Console.ReadLine();
    if (string.IsNullOrEmpty(userInput)) break;
    inputs = [new ChatMessage(ChatRole.User, userInput)];
}

class AutoApproveSubAgentHandle : ISubAgentHandle
{
    public Task<ToolApprovalResponseContent> ApproveToolCallAsync(
        string agentId, ToolApprovalRequestContent call, CancellationToken ct)
        => Task.FromResult(call.CreateResponse(approved: true));

    public Task<object?> ProvideFunctionResultAsync(
        string agentId, FunctionCallContent call, CancellationToken ct)
        => Task.FromResult<object?>(null);
}
```

The agent can plan, read/write files, run commands, and delegate to sub-agents. For a full terminal UI with tool visualization, todo rendering, and reasoning output, see the [Coding Agent sample](#coding-agent) below. To require approval before sensitive tools run, see [Tool approval](#tool-approval).

## Customization

### Sub-agents

Register custom sub-agent types alongside the built-in general-purpose agent:

```csharp
var options = DeepAgentOptionsBuilder.Create()
    .WithSubAgent(new SubAgentProviderOptions
    {
        GeneralPurposeAgent = new GeneralPurposeAgentOptions(handle)
        {
            Description = "General-purpose agent for multi-step tasks.",
            SystemPrompt = "You are an agent completing a delegated task."
        },
        SubAgents =
        [
            new SubAgent(
                Name: "researcher",
                Description: "Specialized agent for research tasks.",
                Handle: handle,
                Factory: new MyResearchAgentFactory())
        ]
    })
    .Build();
```

Implement `ISubAgentFactory` to control how sub-agents are created: provide a custom `IChatClient`, configure agent options, or decorate the agent after construction.

### Filesystem

`FileSystemAccess` provides a sandboxed filesystem rooted at a directory of your choice. By default, all paths are restricted to that root.

```csharp
var options = DeepAgentOptionsBuilder.Create()
    .WithFileSystem(new FileSystemProviderOptions(
        new FileSystemAccess(new DirectoryInfo("/my/project"))))
    .Build();
```

### Compaction

Plug in a `CompactionStrategy` to manage context during long-running autonomous function call loops. The example below uses summarization when the token count exceeds a threshold:

```csharp
using Microsoft.Agents.AI.Compaction;

var options = DeepAgentOptionsBuilder.Create()
    .WithCompaction(new CompactionProviderOptions(
        new PipelineCompactionStrategy(
            [new SummarizationCompactionStrategy(chatClient, CompactionTriggers.TokensExceed(200_000))])))
    .Build();
```

### Extending the agent

Add your own tools and `AIContextProvider`s via `ChatClientAgentOptions`. DeepAgentNet merges them with its built-in providers, so everything is available to the agent:

```csharp
var agent = chatClient.AsDeepAgent(
    agentOptions: new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions
        {
            Instructions = "You are a helpful autonomous agent.",
            Tools = [myCustomTool]
        },
        AIContextProviders = [new MyCustomContextProvider()]
    },
    deepAgentOptions: options);
```

### Tool approval

Built-in tools can require human approval before execution. Configure per-tool policies via `ToolApprovalPolicy`:

```csharp
var options = DeepAgentOptionsBuilder.Create()
    .WithShell(new ShellProviderOptions(new LocalShellResolver())
    {
        ToolOptions = new ToolOptions { ApprovalPolicy = ToolApprovalPolicy.Required }
    })
    .Build();
```

## Sample

### Coding Agent

See [`samples/CodingAgentSample`](samples/CodingAgentSample) for an interactive terminal coding agent built with DeepAgentNet, featuring:

- Streaming responses with reasoning output
- Human-in-the-loop tool approval
- Hierarchical sub-agents (general-purpose + explore)
- Todo list visualization
- Conversation compaction
- Terminal UI with [Spectre.Console](https://spectreconsole.net)

[Demo](https://github.com/user-attachments/assets/9e8155a1-ce9c-4745-bbeb-bfa861da0bfa)
<video src="https://github.com/user-attachments/assets/9e8155a1-ce9c-4745-bbeb-bfa861da0bfa"></video>

### Data Analysis Agent

See [`samples/DataAnalysisAgentSample`](samples/DataAnalysisAgentSample) and [`samples/AzureDynamicSessionsSample`](samples/AzureDynamicSessionsSample) for interactive data analysis agents that query databases, analyze data, and generate visualizations, featuring:

- SQL database tools (schema inspection, query execution) via `SqlDatabaseContextProvider`
- Sandboxed Python code interpreter (statistical analysis, chart generation) via `AzureDynamicSessionsProvider` using [Azure Dynamic Sessions](https://learn.microsoft.com/azure/container-apps/sessions)

Both samples use the [Chinook](https://github.com/lerocha/chinook-database) SQLite database as a demo dataset.

[Demo](https://github.com/user-attachments/assets/b24dd460-abbb-4562-a10d-8832457688f4)
<video src="https://github.com/user-attachments/assets/b24dd460-abbb-4562-a10d-8832457688f4"></video>

## Acknowledgements

This project is inspired by [deepagents](https://github.com/langchain-ai/deepagents) by LangChain, an agent harness providing planning, filesystem, shell, and sub-agent tools out of the box.

## License

This project is licensed under the terms of the [MIT license](LICENSE)
