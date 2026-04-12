using System.Diagnostics;

namespace DeepAgentNet.Shells.Internal
{
    internal class PosixLocalShellRunner : LocalShellRunner
    {
        private readonly string _shellPath;

        public PosixLocalShellRunner(string shellPath, string shell, LocalShellOptions options) : base(shell, options)
        {
            _shellPath = shellPath;
        }

        protected override ProcessStartInfo CreateProcessStartInfo(string command, string workingDirectory) => new()
        {
            FileName = _shellPath,
            ArgumentList = { "-c", command },
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }
}
