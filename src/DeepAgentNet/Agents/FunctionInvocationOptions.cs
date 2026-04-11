namespace DeepAgentNet.Agents
{
    public record FunctionInvocationOptions
    {
        public int MaximumIterationsPerRequest { get; init; } = int.MaxValue;
        public bool AllowConcurrentInvocation { get; init; } = true;
        public int MaximumConsecutiveErrorsPerRequest { get; init; } = 3;
    }
}
