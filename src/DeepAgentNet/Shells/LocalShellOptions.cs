namespace DeepAgentNet.Shells
{
    public record LocalShellOptions
    {
        public HashSet<string> BlacklistedShells { get; init; } = ["fish", "nu"];
        public TimeSpan? DefaultTimeout { get; init; } = null;
        public TimeSpan GracefulTimeout { get; init; } = TimeSpan.FromSeconds(3);
    }
}
