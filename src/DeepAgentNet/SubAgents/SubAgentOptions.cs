using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.TodoLists;

namespace DeepAgentNet.SubAgents
{
    public record SubAgentOptions
    {
        public TodoListProviderOptions? TodoList { get; init; }
        public FileSystemProviderOptions? FileSystem { get; init; }
        public CompactionProviderOptions? Compaction { get; init; }
    }
}
