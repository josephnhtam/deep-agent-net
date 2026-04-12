using DeepAgentNet.Shells.Contracts;
using DeepAgentNet.Shells.Internal;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeepAgentNet.Shells
{
    public class LocalShellResolver : IShellResolver
    {
        private readonly LocalShellOptions _options;

        public LocalShellResolver(LocalShellOptions? options = null)
        {
            _options = options ?? new();
        }

        public List<IShellRunner> ResolveShells()
        {
            var candidates = GetCandidates();

            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            List<IShellRunner> runners = [];

            foreach (string candidate in candidates)
            {
                string? resolvedPath = TryResolve(candidate);

                if (resolvedPath is not null && seen.Add(ShellName(resolvedPath)))
                    runners.Add(CreateShellRunner(resolvedPath));
            }

            if (runners.Count == 0)
            {
                runners.Add(CreateShellRunner(GetFallbackShell()));
            }

            return runners;
        }

        private static string GetFallbackShell()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "cmd.exe";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "/bin/zsh";

            return "/bin/sh";
        }

        private static List<string> GetCandidates()
        {
            List<string> candidates = [Environment.GetEnvironmentVariable("SHELL") ?? string.Empty];

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                candidates.AddRange([
                    "pwsh",
                    "powershell",
                    "bash",
                    Environment.GetEnvironmentVariable("COMSPEC") ?? string.Empty,
                    "cmd.exe"
                ]);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                candidates.AddRange(["bash", "zsh", "/bin/zsh"]);
            }
            else
            {
                candidates.AddRange(["bash", "zsh", "/bin/sh"]);
            }

            candidates.RemoveAll(string.IsNullOrWhiteSpace);
            return candidates;
        }

        private IShellRunner CreateShellRunner(string path)
        {
            string shell = ShellName(path);

            if (shell is "pwsh" or "powershell")
                return new PowerLocalShellRunner(path, shell, _options);

            if (shell is "cmd")
                return new CmdLocalShellRunner(path, shell, _options);

            return new PosixLocalShellRunner(path, shell, _options);
        }

        private string? TryResolve(string? nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath)) return null;

            string? resolvedPath = nameOrPath;

            if (!Path.IsPathRooted(nameOrPath) && !nameOrPath.StartsWith('/'))
            {
                resolvedPath = Which(nameOrPath);
            }

            if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
            {
                string shellName = ShellName(resolvedPath);

                if (!_options.BlacklistedShells.Contains(shellName))
                    return resolvedPath;
            }

            return null;
        }

        private static string ShellName(string nameOrPath) => Path.GetFileNameWithoutExtension(nameOrPath).ToLowerInvariant();

        private string? Which(string command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using Process? process = Process.Start(startInfo);
                if (process == null) return null;

                string? result = process.StandardOutput.ReadLine()?.Trim();
                bool exited = process.WaitForExit(_options.DefaultTimeout ?? TimeSpan.FromSeconds(30));

                if (exited && process.ExitCode == 0 && !string.IsNullOrEmpty(result))
                {
                    return result;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
