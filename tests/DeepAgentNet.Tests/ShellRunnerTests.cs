using DeepAgentNet.Shells;
using DeepAgentNet.Shells.Contracts;
using DeepAgentNet.Tests.Support;
using System.Runtime.InteropServices;
using Xunit;

namespace DeepAgentNet.Tests
{
    public class ShellRunnerTests
    {
        private static IShellRunner CreateRunner(LocalShellOptions? options = null)
        {
            List<IShellRunner> runners = new LocalShellResolver(options).ResolveShells();
            Assert.NotEmpty(runners);
            return runners[0];
        }

        [Fact]
        public async Task RunAsync_Should_ExitZero_When_EchoSucceeds()
        {
            using TempWorkspace workspace = new();
            IShellRunner runner = CreateRunner();

            string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "echo hello"
                : "echo hello";

            CommandResult result = await runner.RunAsync(command, workspace.Root.FullName);

            Assert.False(result.TimedOut);
            Assert.False(result.Aborted);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("hello", result.Output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RunAsync_Should_SetTimedOut_When_CommandExceedsTimeout()
        {
            using TempWorkspace workspace = new();
            IShellRunner runner = CreateRunner(new LocalShellOptions
            {
                DefaultTimeout = TimeSpan.FromSeconds(30)
            });

            string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "powershell -NoProfile -Command \"Start-Sleep -Seconds 10\""
                : "sleep 10";

            CommandResult result = await runner.RunAsync(
                command,
                workspace.Root.FullName,
                timeout: TimeSpan.FromMilliseconds(500));

            Assert.True(result.TimedOut);
            Assert.False(result.Aborted);
        }

        [Fact]
        public async Task RunAsync_Should_SetAborted_When_CancellationRequested()
        {
            using TempWorkspace workspace = new();
            IShellRunner runner = CreateRunner();

            string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "powershell -NoProfile -Command \"Start-Sleep -Seconds 10\""
                : "sleep 10";

            using CancellationTokenSource cts = new();
            Task<CommandResult> runTask = runner.RunAsync(command, workspace.Root.FullName, cancellation: cts.Token).AsTask();
            await Task.Delay(100);
            cts.Cancel();

            CommandResult result = await runTask;

            Assert.True(result.Aborted);
            Assert.False(result.TimedOut);
        }

        [Fact]
        public async Task RunAsync_Should_UseWorkingDirectory_When_CwdIsProvided()
        {
            using TempWorkspace workspace = new();
            IShellRunner runner = CreateRunner();

            string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "cd"
                : "pwd";

            CommandResult result = await runner.RunAsync(command, workspace.Root.FullName);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                Path.GetFullPath(workspace.Root.FullName).TrimEnd(Path.DirectorySeparatorChar),
                result.Output.Replace('\\', '/').Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RunAsync_Should_ReturnNonZeroExitCode_When_CommandFails()
        {
            using TempWorkspace workspace = new();
            IShellRunner runner = CreateRunner();

            string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "exit /b 1"
                : "false";

            CommandResult result = await runner.RunAsync(command, workspace.Root.FullName);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.TimedOut);
            Assert.False(result.Aborted);
        }

        [Fact]
        public async Task RunAsync_Should_CaptureStderr_When_CommandWritesToStderr()
        {
            using TempWorkspace workspace = new();
            IShellRunner runner = CreateRunner();

            string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "echo err 1>&2"
                : "echo err >&2";

            CommandResult result = await runner.RunAsync(command, workspace.Root.FullName);

            Assert.Contains("err", result.Output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RunAsync_Should_SetTimedOut_When_DefaultTimeoutFromOptionsExceeded()
        {
            using TempWorkspace workspace = new();
            IShellRunner runner = CreateRunner(new LocalShellOptions
            {
                DefaultTimeout = TimeSpan.FromMilliseconds(500)
            });

            string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "powershell -NoProfile -Command \"Start-Sleep -Seconds 10\""
                : "sleep 10";

            CommandResult result = await runner.RunAsync(command, workspace.Root.FullName);

            Assert.True(result.TimedOut);
            Assert.False(result.Aborted);
        }
    }
}
