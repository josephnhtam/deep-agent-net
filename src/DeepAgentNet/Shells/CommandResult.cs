using System.ComponentModel;

namespace DeepAgentNet.Shells
{
    [Description("The result of a shell command execution")]
    public record CommandResult(
        [Description("The output of the command, including both stdout and stderr")]
        string Output,
        [Description("The exit code of the command, or null if the command did not complete")]
        int? ExitCode,
        [Description("Whether the command timed out before completing")]
        bool TimedOut,
        [Description("Whether the command was aborted before completing")]
        bool Aborted);
}
