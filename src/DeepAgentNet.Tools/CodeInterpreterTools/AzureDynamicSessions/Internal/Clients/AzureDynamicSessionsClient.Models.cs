namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients
{
    internal partial class AzureDynamicSessionsClient
    {
        private record ValueResponse<T>(IReadOnlyList<T> Value);
    }

    internal record SessionFile(string Name, string Type, long? SizeInBytes, DateTimeOffset LastModifiedAt);

    internal record CodeExecutionResult(
        string Status,
        string? Stdout,
        string? Stderr,
        object? ExecutionResult,
        long? ExecutionTimeInMilliseconds);
}
