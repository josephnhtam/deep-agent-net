using Microsoft.Extensions.Logging;

namespace DeepAgentNet.Shells.Internal.Tools
{
    internal static partial class ShellToolLoggerExtensions
    {
        [LoggerMessage(LogLevel.Debug, "Executing shell command: shell={Shell}, cwd={WorkingDirectory}, command={Command}")]
        public static partial void ExecutingShellCommand(this ILogger<ShellToolProvider> logger, string shell, string command, string workingDirectory);

        [LoggerMessage(LogLevel.Debug, "Shell command completed: shell={Shell}, exitCode={ExitCode}")]
        public static partial void ShellCommandCompleted(this ILogger<ShellToolProvider> logger, string shell, int? exitCode);

        [LoggerMessage(LogLevel.Warning, "Shell command timed out: shell={Shell}, command={Command}")]
        public static partial void ShellCommandTimedOut(this ILogger<ShellToolProvider> logger, string shell, string command);

        [LoggerMessage(LogLevel.Warning, "Shell command aborted: shell={Shell}, command={Command}")]
        public static partial void ShellCommandAborted(this ILogger<ShellToolProvider> logger, string shell, string command);

        [LoggerMessage(LogLevel.Warning, "Unknown shell requested: {ShellName}")]
        public static partial void UnknownShell(this ILogger<ShellToolProvider> logger, string shellName);
    }
}
