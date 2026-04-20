# DeepAgentNet

### The agent harness for .NET.

DeepAgentNet is an agent harness for .NET — a ready-to-run framework for building autonomous agents. Instead of wiring up prompts, tools, and context management yourself, you get a working agent out of the box and customize what you need.

Built on [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp) and [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions).

**What's included:**

- **Planning** — `write_todos` for task breakdown and progress tracking
- **Filesystem** — `read_file`, `write_file`, `edit_file`, `delete_file`, `ls`, `glob`, `grep` for sandboxed file access
- **Shell** — `shell` for running commands with cross-platform shell detection
- **Sub-agents** — `task` for delegating work with isolated context windows and session resume
- **Context management** — chat history and compaction integrated at the chat client level for automatic context management during autonomous function calls
- **Tool approval** — human-in-the-loop gates for sensitive operations

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

## License

This project is licensed under the terms of the [MIT license](https://github.com/josephnhtam/deep-agent-net/blob/main/LICENSE)
