using DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors
{
    public record SqlExecutorOptions
    {
        public TimeSpan? DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);
        public ISqlInterceptor? Interceptor { get; init; }
    }
}
