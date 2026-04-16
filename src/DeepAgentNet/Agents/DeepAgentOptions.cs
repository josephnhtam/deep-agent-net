using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.Shells;
using DeepAgentNet.SubAgents;
using DeepAgentNet.TodoLists;

namespace DeepAgentNet.Agents
{
    public class DeepAgentOptions
    {
        public TodoListProviderOptions? TodoList { get; init; }
        public SubAgentProviderOptions? SubAgent { get; init; }
        public FileSystemProviderOptions? FileSystem { get; init; }
        public CompactionProviderOptions? Compaction { get; init; }
        public ShellProviderOptions? Shell { get; init; }
        public FunctionInvocationOptions FunctionInvocation { get; init; } = new();

        internal DeepAgentOptions() { }
    }
}
