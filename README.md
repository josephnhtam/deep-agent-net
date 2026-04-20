# DeepAgentNet

### The agent harness for .NET.

[![NuGet](https://img.shields.io/nuget/v/DeepAgentNet.svg)](https://www.nuget.org/packages/DeepAgentNet)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

DeepAgentNet is an agent harness for .NET — a ready-to-run framework for building autonomous agents. Instead of wiring up prompts, tools, and context management yourself, you get a working agent out of the box and customize what you need.

Built on [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp) and [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions).

**What's included:**

- **Planning** — `write_todos` for task breakdown and progress tracking
- **Filesystem** — `read_file`, `write_file`, `edit_file`, `delete_file`, `ls`, `glob`, `grep` for sandboxed file access
- **Shell** — `shell` for running commands with cross-platform shell detection
- **Sub-agents** — `task` for delegating work with isolated context windows and session resume
- **Context management** — chat history and compaction integrated at the chat client level for automatic context management during autonomous function calls
- **Tool approval** — human-in-the-loop gates for sensitive operations

## Installation

```
dotnet add package DeepAgentNet
```

## Quick Start

```csharp
using DeepAgentNet.Agents;
using DeepAgentNet.FileSystems;
using DeepAgentNet.Shells;
using DeepAgentNet.SubAgents;
using DeepAgentNet.SubAgents.Contracts;
using DeepAgentNet.TodoLists;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// 1. Create any IChatClient (Azure OpenAI, OpenAI, Ollama, etc.)
IChatClient chatClient = /* your chat client */;

// 2. Implement ISubAgentHandle to control sub-agent behavior
ISubAgentHandle subAgentHandle = new MySubAgentHandle();

// 3. Configure the agent harness
var options = DeepAgentOptionsBuilder.Create()
    .WithTodoList()
    .WithFileSystem(new FileSystemProviderOptions(
        new FileSystemAccess(new DirectoryInfo("./workspace"))))
    .WithShell(new ShellProviderOptions(new LocalShellResolver()))
    .WithSubAgent(new SubAgentProviderOptions
    {
        GeneralPurposeAgent = new GeneralPurposeAgentOptions(subAgentHandle)
    })
    .Build();

// 4. Build the agent
var agent = chatClient.AsDeepAgent(
    agentOptions: new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions
        {
            Instructions = "You are a helpful autonomous agent."
        }
    },
    deepAgentOptions: options);

// 5. Create a session and run
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

    var response = updates.ToAgentResponse();
    var approvals = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<ToolApprovalRequestContent>()
        .ToList();

    // Handle tool approval requests from the master agent
    if (approvals.Count > 0)
    {
        var results = new List<AIContent>();
        foreach (var approval in approvals)
        {
            Console.Write($"\nApprove {approval.ToolCall}? (y/n): ");
            bool approved = Console.ReadLine()?.Trim().ToLower() == "y";
            results.Add(approval.CreateResponse(approved));
        }
        inputs = [new ChatMessage(ChatRole.Tool, results)];
        continue;
    }

    Console.Write("\nYou: ");
    var userInput = Console.ReadLine();
    if (string.IsNullOrEmpty(userInput)) break;
    inputs = [new ChatMessage(ChatRole.User, userInput)];
}

// ISubAgentHandle implementation
class MySubAgentHandle : ISubAgentHandle
{
    // Automatically approve all sub-agent tool calls for demo
    public Task<ToolApprovalResponseContent> ApproveToolCallAsync(
        string agentId, ToolApprovalRequestContent call, CancellationToken cancellationToken)
        => Task.FromResult(call.CreateResponse(approved: true));

    public Task<object?> ProvideFunctionResultAsync(
        string agentId, FunctionCallContent call, CancellationToken cancellationToken)
        => Task.FromResult<object?>(null);
}
```

The agent can plan, read/write files, run commands, and delegate to sub-agents. Add tools, customize prompts, or swap models as needed.

## Customization

### Sub-agents

Register custom sub-agent types alongside the built-in general-purpose agent:

```csharp
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
```

Implement `ISubAgentFactory` to control how sub-agents are created — provide a custom `IChatClient`, configure agent options, or decorate the agent after construction.

### Filesystem

`FileSystemAccess` provides a sandboxed filesystem rooted at a directory of your choice. By default, all paths are restricted to that root.

```csharp
.WithFileSystem(new FileSystemProviderOptions(
    new FileSystemAccess(new DirectoryInfo("/my/project"))))
```

### Compaction

Plug in a `CompactionStrategy` to manage context during long-running autonomous function call loops. The example below uses summarization when the token count exceeds a threshold:

```csharp
using Microsoft.Agents.AI.Compaction;

.WithCompaction(new CompactionProviderOptions(
    new PipelineCompactionStrategy(
        [new SummarizationCompactionStrategy(chatClient, CompactionTriggers.TokensExceed(200_000))])))
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
.WithShell(new ShellProviderOptions(new LocalShellResolver())
{
    ToolOptions = new ToolOptions { ApprovalPolicy = ToolApprovalPolicy.Required }
})
```

## Sample

See [`samples/CodingAgentSample`](samples/CodingAgentSample) for an interactive terminal coding agent built with DeepAgentNet, featuring:

- Streaming responses with reasoning output
- Human-in-the-loop tool approval
- Hierarchical sub-agents (general-purpose + explore)
- Todo list visualization
- Conversation compaction
- Terminal UI with [Spectre.Console](https://spectreconsole.net)

## Acknowledgements

This project is inspired by [Deep Agents](https://github.com/langchain-ai/deepagents) by LangChain — an agent harness providing planning, filesystem, shell, and sub-agent tools out of the box.

## License

This project is licensed under the terms of the [MIT license](LICENSE)
