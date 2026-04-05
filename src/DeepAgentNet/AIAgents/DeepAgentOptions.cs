using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.SubAgents;
using DeepAgentNet.TodoLists;

namespace DeepAgentNet.AIAgents
{
    public record DeepAgentOptions
    {
        public TodoListProviderOptions? TodoList { get; init; }
        public SubAgentProviderOptions? SubAgent { get; init; }
        public FileSystemProviderOptions? FileSystem { get; init; }
        public CompactionProviderOptions? Compaction { get; init; }
    }
}
