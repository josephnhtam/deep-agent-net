using DeepAgentNet.Shells.Internal;

namespace DeepAgentNet.Shells.Contracts
{
    public interface IShellRunner
    {
        string Shell { get; }
        ValueTask<CommandResult> RunAsync(string command, string workingDirectory, TimeSpan? timeout = null, CancellationToken cancellation = default);
    }
}
