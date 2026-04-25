using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal.Contracts;
using DeepAgentNet.Shells.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace DeepAgentNet.Shells.Internal.Tools
{
    internal class ShellToolProvider : IToolProvider
    {
        private readonly IReadOnlyDictionary<string, IShellRunner> _shellRunners;
        private readonly IReadOnlyList<string> _shellNames;
        private readonly string? _defaultWorkingDirectory;
        private readonly ILogger<ShellToolProvider>? _logger;

        public AITool Tool { get; }

        public ShellToolProvider(IReadOnlyList<IShellRunner> shellRunners, ShellProviderOptions options, ILoggerFactory? loggerFactory = null)
        {
            _shellRunners = shellRunners.ToDictionary(r => r.Shell, StringComparer.OrdinalIgnoreCase);
            _shellNames = shellRunners.Select(r => r.Shell).ToList();
            _defaultWorkingDirectory = options.DefaultWorkingDirectory;
            _logger = loggerFactory?.CreateLogger<ShellToolProvider>();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = ShellDefaults.ToolName,
                Description = options.Description?.Invoke(_shellNames) ?? ShellDefaults.GetToolDescription(_shellNames),
                JsonSchemaCreateOptions = CreateJsonSchemaOptions(_shellNames)
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private AIJsonSchemaCreateOptions CreateJsonSchemaOptions(IReadOnlyList<string> shellNames) => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "shellName" => $"The name of the shell to use. Available: {string.Join(", ", shellNames.Select(s => $"'{s}'"))}",
                "cwdPath" => $"The working directory in which to run the command. Defaults to '{_defaultWorkingDirectory ?? Directory.GetCurrentDirectory()}'",
                _ => null
            }
        };

        private async ValueTask<CommandResult> ExecuteAsync(
            string shellName,
            [Description("The shell command to execute")]
            string command,
            string? cwdPath = null,
            [Description("Optional timeout for the command")]
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (!_shellRunners.TryGetValue(shellName, out IShellRunner? runner))
            {
                _logger?.UnknownShell(shellName);

                throw new ArgumentException(
                    $"Error: Shell '{shellName}' is not recognized. Available shells: {string.Join(", ", _shellNames.Select(s => $"'{s}'"))}",
                    nameof(shellName));
            }

            string resolvedDir = cwdPath ?? _defaultWorkingDirectory ?? Directory.GetCurrentDirectory();

            if (!Path.IsPathFullyQualified(resolvedDir) && _defaultWorkingDirectory is not null)
                resolvedDir = Path.GetFullPath(Path.Combine(_defaultWorkingDirectory, resolvedDir));

            _logger?.ExecutingShellCommand(shellName, command, resolvedDir);

            CommandResult result = await runner.RunAsync(command, resolvedDir, timeout, cancellationToken);

            if (result.TimedOut)
                _logger?.ShellCommandTimedOut(shellName, command);
            else if (result.Aborted)
                _logger?.ShellCommandAborted(shellName, command);
            else
                _logger?.ShellCommandCompleted(shellName, result.ExitCode);

            return result;
        }
    }
}
