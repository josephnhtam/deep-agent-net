namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors
{
    public record SqlExecutorOptions
    {
        public TimeSpan? DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
