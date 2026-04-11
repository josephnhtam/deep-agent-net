using DeepAgentNet.Agents.Internal;
using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.SubAgents;
using DeepAgentNet.TodoLists;

namespace DeepAgentNet.Agents
{
    public class DeepAgentOptionsBuilder
    {
        private TodoListProviderOptions? _todoListOptions;
        private SubAgentProviderOptions? _subAgentOptions;
        private FileSystemProviderOptions? _fileSystemOptions;
        private CompactionProviderOptions? _compactionOptions;
        private FunctionInvocationOptions? _functionInvocationOptions;

        public static DeepAgentOptionsBuilder Create() => new();

        private DeepAgentOptionsBuilder() { }

        public DeepAgentOptionsBuilder WithTodoList(TodoListProviderOptions? options = null)
        {
            _todoListOptions = options ?? new();
            return this;
        }

        public DeepAgentOptionsBuilder WithSubAgent(SubAgentProviderOptions options)
        {
            _subAgentOptions = options;
            return this;
        }

        public DeepAgentOptionsBuilder WithFileSystem(FileSystemProviderOptions options)
        {
            _fileSystemOptions = options;
            return this;
        }

        public DeepAgentOptionsBuilder WithCompaction(CompactionProviderOptions options)
        {
            _compactionOptions = options;
            return this;
        }

        public DeepAgentOptionsBuilder ConfigureFunctionInvocation(FunctionInvocationOptions options)
        {
            _functionInvocationOptions = options;
            return this;
        }

        public DeepAgentOptions Build()
        {
            return new DeepAgentOptions
            {
                TodoList = _todoListOptions,
                SubAgent = _subAgentOptions,
                FileSystem = _fileSystemOptions,
                Compaction = _compactionOptions,
                FunctionInvocation = _functionInvocationOptions ?? new()
            };
        }
    }
}
