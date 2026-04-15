using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.Logging;

namespace DeepAgentNet.Compactions
{
    public record CompactionProviderOptions(CompactionStrategy CompactionStrategy)
    {
        public ChatHistoryProvider? ChatHistoryProvider { get; init; }
        public string? CompactionStateKey { get; init; }
        public ILoggerFactory? LoggerFactory { get; init; }
    }
}
