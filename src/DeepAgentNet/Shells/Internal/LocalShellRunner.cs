using DeepAgentNet.Shells.Contracts;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DeepAgentNet.Shells.Internal
{
    internal abstract class LocalShellRunner : IShellRunner
    {
        public string Shell { get; }

        private readonly LocalShellOptions _options;

        protected LocalShellRunner(string shell, LocalShellOptions options)
        {
            Shell = shell;
            _options = options;
        }

        protected abstract ProcessStartInfo CreateProcessStartInfo(string command, string workingDirectory);

        public async ValueTask<CommandResult> RunAsync(
            string command, string workingDirectory, TimeSpan? timeout = null, CancellationToken cancellation = default)
        {
            ProcessStartInfo startInfo = CreateProcessStartInfo(command, workingDirectory);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            StringBuilder output = new StringBuilder();
            TaskCompletionSource tcs = new TaskCompletionSource();

            using var process = StartProcess(startInfo, output, tcs);

            TimeSpan? effectiveTimeout = timeout ?? _options.DefaultTimeout;
            using var timeoutCts = effectiveTimeout.HasValue ? new CancellationTokenSource(effectiveTimeout.Value) : new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeoutCts.Token);

            bool timedOut = false, aborted = false;

            try
            {
                await tcs.Task.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                timedOut = timeoutCts.IsCancellationRequested;
                aborted = cancellation.IsCancellationRequested;
            }
            finally
            {
                if (!process.HasExited)
                {
                    try
                    {
                        await KillProcessGracefully(process);
                    }
                    catch
                    {
                        KillProcess(process);
                    }
                }
            }

            int? exitCode = process.HasExited ? process.ExitCode : null;
            return new CommandResult(output.ToString(), exitCode, timedOut, aborted);
        }

        private static Process StartProcess(ProcessStartInfo startInfo, StringBuilder output, TaskCompletionSource tcs)
        {
            Process process = new()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.Exited += (_, _) =>
            {
                tcs.TrySetResult();
            };

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data == null)
                    return;

                lock (output)
                {
                    output.AppendLine(args.Data);
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data == null)
                    return;

                lock (output)
                {
                    output.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }

        private async Task KillProcessGracefully(Process process)
        {
            if (process.HasExited)
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using Process? killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    ArgumentList = { "/pid", process.Id.ToString(), "/f", "/t" },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                if (killer != null)
                {
                    using CancellationTokenSource cts = new(_options.GracefulTimeout);
                    await killer.WaitForExitAsync(cts.Token);
                }
            }
            else
            {
                using Process? termKill = Process.Start(new ProcessStartInfo
                {
                    FileName = "kill",
                    ArgumentList = { "-TERM", $"-{process.Id}" },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                await Task.Delay(_options.GracefulTimeout);

                if (!process.HasExited)
                {
                    using Process? kill = Process.Start(new ProcessStartInfo
                    {
                        FileName = "kill",
                        ArgumentList = { "-KILL", process.Id.ToString() },
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                }
            }
        }

        private static void KillProcess(Process process)
        {
            try { process.Kill(true); }
            catch { }
        }
    }
}
