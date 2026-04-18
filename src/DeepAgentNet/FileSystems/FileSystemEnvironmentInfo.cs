using System.Runtime.InteropServices;

namespace DeepAgentNet.FileSystems
{
    public record FileSystemEnvironmentInfo
    {
        public required string WorkingDirectory { get; init; }
        public string? OsDescription { get; init; }

        public static FileSystemEnvironmentInfo Create(string workingDirectory) => new()
        {
            WorkingDirectory = workingDirectory,
            OsDescription = RuntimeInformation.OSDescription
        };
    }
}
