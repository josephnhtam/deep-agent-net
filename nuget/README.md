# DeepAgentNet

### The agent harness for .NET.

DeepAgentNet is an agent harness for .NET. A ready-to-run autonomous agent framework. Instead of wiring up prompts, tools, and context management yourself, you get a working agent immediately and customize what you need.

Built on [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp) and [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions). Provider-agnostic — works with any `IChatClient`.

**What's included:**

- **Planning** — `write_todos` for task breakdown and progress tracking
- **Filesystem** — `read_file`, `write_file`, `edit_file`, `ls`, `glob`, `grep` for reading and writing context
- **Shell access** — `shell` for running commands across platforms
- **Sub-agents** — `task` for delegating work with isolated context windows and session resume
- **Context management** — integrates Agent Framework's compaction strategies at the chat client level for automatic conversation management
- **Tool approval** — human-in-the-loop gates for sensitive operations
- **Smart defaults** — prompts, tool descriptions, and safety guards that work out of the box

## Quick Start

```csharp
using DeepAgentNet.Agents;
using DeepAgentNet.FileSystems;
using DeepAgentNet.Shells;
using DeepAgentNet.SubAgents;
using DeepAgentNet.TodoLists;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

IChatClient chatClient = /* your chat client */;

var options = DeepAgentOptionsBuilder.Create()
    .WithTodoList()
    .WithFileSystem(new FileSystemProviderOptions(
        new FileSystemAccess(new DirectoryInfo("./workspace"))))
    .WithShell(new ShellProviderOptions(new LocalShellResolver()))
    .WithSubAgent(new SubAgentProviderOptions
    {
        GeneralPurposeAgent = new GeneralPurposeAgentOptions(mySubAgentHandle)
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

await foreach (var update in agent.RunStreamingAsync(
    [new ChatMessage(ChatRole.User, "Hello!")], session))
{
    Console.Write(update.Text);
}
```

## Documentation

For full documentation, architecture details, customization guides, and samples, visit the [GitHub repository](https://github.com/josephnhtam/deep-agent-net).

## License

[MIT](https://github.com/josephnhtam/deep-agent-net/blob/main/LICENSE)
