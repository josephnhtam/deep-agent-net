using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.SubAgents;
using DeepAgentNet.TodoLists;

namespace DeepAgentNet.Agents
{
    public record DeepAgentOptions
    {
        public TodoListProviderOptions? TodoList { get; init; }
        public SubAgentProviderOptions? SubAgent { get; init; }
        public FileSystemProviderOptions? FileSystem { get; init; }
        public CompactionProviderOptions? Compaction { get; init; }

        public int MaximumIterationsPerRequest { get; init; } = int.MaxValue;
        public bool AllowConcurrentInvocation { get; init; } = true;
        public int MaximumConsecutiveErrorsPerRequest { get; init; } = 3;
    }
}
