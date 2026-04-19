using Microsoft.Extensions.Logging;

namespace DeepAgentNet.SubAgents.Internal.Tools
{
    internal static partial class RunSubAgentToolProviderLoggerExtensions
    {
        [LoggerMessage(LogLevel.Debug, "Executing sub-agent tool: type={SubAgentType}, taskId={TaskId}, description={Description}")]
        public static partial void ExecutingSubAgent(this ILogger<RunSubAgentToolProvider> logger, string subAgentType, string? taskId, string description);

        [LoggerMessage(LogLevel.Warning, "Unknown sub-agent type requested: {SubAgentType}")]
        public static partial void UnknownSubAgentType(this ILogger<RunSubAgentToolProvider> logger, string subAgentType);

        [LoggerMessage(LogLevel.Information, "Sub-agent {SubAgentType} session {TaskId} created")]
        public static partial void SubAgentSessionCreated(this ILogger<RunSubAgentToolProvider> logger, string subAgentType, string taskId);

        [LoggerMessage(LogLevel.Information, "Sub-agent {SubAgentType} session {TaskId} resumed")]
        public static partial void SubAgentSessionResumed(this ILogger<RunSubAgentToolProvider> logger, string subAgentType, string taskId);

        [LoggerMessage(LogLevel.Information, "Sub-agent {SubAgentType} session {TaskId} completed")]
        public static partial void SubAgentSessionCompleted(this ILogger<RunSubAgentToolProvider> logger, string subAgentType, string taskId);

        [LoggerMessage(LogLevel.Debug, "Deserializing sub-agent session {TaskId} of type {SubAgentType}")]
        public static partial void DeserializingSubAgentSession(this ILogger<RunSubAgentToolProvider> logger, string taskId, string subAgentType);

        [LoggerMessage(LogLevel.Debug, "Created new sub-agent session {TaskId} of type {SubAgentType}")]
        public static partial void CreatedNewSubAgentSession(this ILogger<RunSubAgentToolProvider> logger, string taskId, string subAgentType);

        [LoggerMessage(LogLevel.Warning, "Failed to serialize sub-agent session for task {TaskId}; session will not be resumable")]
        public static partial void FailedToSerializeSubAgentSession(this ILogger<RunSubAgentToolProvider> logger, Exception ex, string taskId);
    }
}
