using DeepAgentNet.TodoLists;
using Microsoft.Extensions.AI;
using Spectre.Console;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace SampleUtilities.AgentConsoles
{
    public class AgentConsole
    {
        private readonly string _title;
        private readonly IAgentTurnRunner _agentTurnRunner;
        private readonly ChannelReader<AgentEvent> _channel;

        private bool _inThinking;

        public AgentConsole(string title, IAgentTurnRunner agentTurnRunner, ChannelReader<AgentEvent> channel)
        {
            _title = title;
            _agentTurnRunner = agentTurnRunner;
            _channel = channel;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            AnsiConsole.Write(new Rule($"[cyan bold]{_title}[/]").LeftJustified());

            while (!cancellationToken.IsCancellationRequested)
            {
                string userInput;
                try
                {
                    Console.WriteLine();
                    userInput = await AnsiConsole.AskAsync<string>("[cyan bold]You:[/] ", cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    break;

                try
                {
                    var agentTask = _agentTurnRunner.RunAsync(userInput, cancellationToken);
                    var processEventsTask = ProcessEventsAsync(_channel, agentTask, cancellationToken);
                    await Task.WhenAll(agentTask, processEventsTask);

                    Console.WriteLine();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    EndThinking();
                    AnsiConsole.MarkupLine($"\n[red]Error: {Markup.Escape(ex.Message)}[/]");
                }
                finally
                {
                    EndThinking();
                }
            }

            return;

            async Task ProcessEventsAsync(ChannelReader<AgentEvent> reader, Task agentTask, CancellationToken ct)
            {
                while (!ct.IsCancellationRequested)
                {
                    if (reader.TryRead(out var evt))
                    {
                        if (evt is AgentTurnComplete)
                            break;

                        RenderEvent(evt);
                    }
                    else
                    {
                        if (agentTask.IsCompleted && !reader.TryPeek(out _))
                            break;

                        try
                        {
                            await Task.WhenAny(reader.WaitToReadAsync(ct).AsTask(), agentTask);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private void RenderEvent(AgentEvent evt)
        {
            switch (evt)
            {
                case AgentThinkingDelta { Text: var text }:
                    if (!_inThinking)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write("\nThinking: ");
                        _inThinking = true;
                    }

                    Console.Write(text);
                    break;

                case AgentNewMessage:
                    EndThinking();
                    Console.Write('\n');
                    AnsiConsole.Markup("[green bold]Agent:[/] ");
                    break;

                case AgentTextDelta { Text: var text }:
                    EndThinking();
                    Console.Write(text);
                    break;

                case TodosUpdated { Todos: var todos }:
                    EndThinking();
                    RenderTodos(todos);
                    break;

                case ToolUsed { Call: var call, Result: var result }:
                    EndThinking();
                    ToolUsed(call);
                    break;

                case ApprovalRequired ar:
                    EndThinking();
                    HandleApproval(ar);
                    break;

                case SubAgentStarted { Description: var desc }:
                    Console.WriteLine();
                    AnsiConsole.MarkupLine($"  [yellow]▸[/] [grey]Sub-agent:[/] [blue]{Markup.Escape(desc)}[/]");
                    break;

                case SubAgentCompleted { Description: var desc }:
                    AnsiConsole.MarkupLine($"  [green]✓[/] [grey]Completed:[/] [blue]{Markup.Escape(desc)}[/]");
                    break;
            }
        }

        private void EndThinking()
        {
            if (!_inThinking)
                return;

            Console.ResetColor();
            _inThinking = false;
        }

        private void RenderTodos(List<Todo> todos)
        {
            Console.WriteLine();

            var lines = todos.Select(t =>
            {
                var icon = t.Status switch
                {
                    TodoStatus.Completed => "[green]✓[/]",
                    TodoStatus.InProgress => "[blue]▸[/]",
                    TodoStatus.Cancelled => "[red]✕[/]",
                    _ => "[grey]○[/]"
                };
                return $"  {icon} {Markup.Escape(t.Content)}";
            });

            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
                .Header("[yellow bold]Todo List[/]")
                .BorderColor(Color.Yellow)
                .Padding(1, 0));

            Console.WriteLine();
        }

        private static void ToolUsed(FunctionCallContent call)
        {
            var toolName = call.Name;
            var detailLines = new StringBuilder();

            AnsiConsole.MarkupLine($"\n[blue]Tool Used:[/] [yellow]{Markup.Escape(toolName)}[/]");

            if (call.Arguments is { Count: > 0 } args)
            {
                foreach (var (key, value) in args)
                {
                    var display = FormatArgValue(value);
                    detailLines.AppendLine($"  [grey]{Markup.Escape(key)}:[/] {Markup.Escape(display)}");
                }

                AnsiConsole.Write(new Panel(new Markup(detailLines.ToString()))
                    .BorderColor(Color.Grey)
                    .Padding(1, 0));
            }
        }

        private void HandleApproval(ApprovalRequired approvalRequired)
        {
            Console.WriteLine();
            AnsiConsole.Write(new Rule("[red bold]Tool Approval Required[/]").RuleStyle("red"));

            string toolName = "unknown";
            var detailLines = new StringBuilder();

            if (approvalRequired.Request.ToolCall is FunctionCallContent call)
            {
                toolName = call.Name;
                if (call.Arguments is { Count: > 0 } args)
                {
                    foreach (var (key, value) in args)
                    {
                        var display = FormatArgValue(value);
                        detailLines.AppendLine($"  [grey]{Markup.Escape(key)}:[/] {Markup.Escape(display)}");
                    }
                }
            }

            AnsiConsole.Write(new Panel(
                    new Markup($"[bold white]{Markup.Escape(toolName)}[/]\n{detailLines}"))
                .BorderColor(Color.Red)
                .Padding(1, 0));

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Approve this tool call?[/]")
                    .AddChoices("Approve", "Reject"));

            bool approved = choice == "Approve";
            approvalRequired.Completion.SetResult(approvalRequired.Request.CreateResponse(approved));

            AnsiConsole.MarkupLine(approved
                ? "[green]  ✓ Approved[/]"
                : "[red]  ✕ Rejected[/]");

            AnsiConsole.Write(new Rule().RuleStyle("red"));
            Console.WriteLine();
        }

        private static string FormatArgValue(object? value)
        {
            var str = value switch
            {
                JsonElement el => el.ValueKind == JsonValueKind.String ? el.GetString() ?? "null" : el.GetRawText(),
                _ => value?.ToString() ?? "null"
            };

            return str.Length > 120 ? str[..120] + "..." : str;
        }
    }
}
