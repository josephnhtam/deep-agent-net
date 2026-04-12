using DeepAgentNet.Agents;
using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.Shells;
using DeepAgentNet.TodoLists;

namespace DeepAgentNet.SubAgents
{
    public class SubAgentOptions
    {
        public TodoListProviderOptions? TodoList { get; init; }
        public FileSystemProviderOptions? FileSystem { get; init; }
        public CompactionProviderOptions? Compaction { get; init; }
        public ShellProviderOptions? Shell { get; init; }
        public FunctionInvocationOptions? FunctionInvocation { get; init; } = new();

        internal SubAgentOptions() { }
    }
}
