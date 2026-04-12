using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Contracts;
using DeepAgentNet.Shells.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.Shells.Internal.Tools
{
    public class ShellToolProvider : IToolProvider
    {
        private readonly IReadOnlyDictionary<string, IShellRunner> _shellRunners;
        private readonly IReadOnlyList<string> _shellNames;

        public AITool Tool { get; }

        public ShellToolProvider(IReadOnlyList<IShellRunner> shellRunners, ShellProviderOptions options)
        {
            _shellRunners = shellRunners.ToDictionary(r => r.Shell, StringComparer.OrdinalIgnoreCase);
            _shellNames = shellRunners.Select(r => r.Shell).ToList();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = ShellDefaults.ToolName,
                Description = options.Description?.Invoke(_shellNames) ?? ShellDefaults.GetToolDescription(_shellNames),
                JsonSchemaCreateOptions = JsonSchemaCreateOptions(_shellNames)
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private static AIJsonSchemaCreateOptions JsonSchemaCreateOptions(IReadOnlyList<string> shellNames) => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "shellName" => $"The name of the shell to use. Available: {string.Join(", ", shellNames.Select(s => $"'{s}'"))}",
                _ => null
            }
        };

        private async ValueTask<CommandResult> ExecuteAsync(
            string shellName,
            [Description("The shell command to execute")]
            string command,
            [Description("The absolute path to the working directory in which to run the command. Use this instead of cd.")]
            string workingDirectory,
            [Description("Optional timeout for the command")]
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (!_shellRunners.TryGetValue(shellName, out IShellRunner? runner))
            {
                throw new ArgumentException(
                    $"Error: Shell '{shellName}' is not recognized. Available shells: {string.Join(", ", _shellNames.Select(s => $"'{s}'"))}",
                    nameof(shellName));
            }

            return await runner.RunAsync(command, workingDirectory, timeout, cancellationToken);
        }
    }
}
