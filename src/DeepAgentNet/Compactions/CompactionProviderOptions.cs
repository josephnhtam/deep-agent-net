using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.Logging;

namespace DeepAgentNet.Compactions
{
    public record CompactionProviderOptions(CompactionStrategy CompactionStrategy)
    {
        public string? CompactionStateKey { get; init; }
        public ILoggerFactory? LoggerFactory { get; init; }
    }
}
