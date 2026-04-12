using System.Diagnostics;

namespace DeepAgentNet.Shells.Internal
{
    internal class PowerLocalShellRunner : LocalShellRunner
    {
        private readonly string _shellPath;

        public PowerLocalShellRunner(string shellPath, string shell, LocalShellOptions options) : base(shell, options)
        {
            _shellPath = shellPath;
        }

        protected override ProcessStartInfo CreateProcessStartInfo(string command, string workingDirectory) => new()
        {
            FileName = _shellPath,
            ArgumentList = { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", command },
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }
}
