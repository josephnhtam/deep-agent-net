using DeepAgentNet.Compactions;
using DeepAgentNet.FileSystems;
using DeepAgentNet.TodoLists;

namespace DeepAgentNet.SubAgents
{
    public class SubAgentOptionsBuilder
    {
        private TodoListProviderOptions? _todoListOptions;
        private FileSystemProviderOptions? _fileSystemOptions;
        private CompactionProviderOptions? _compactionOptions;

        public static SubAgentOptionsBuilder Create() => new();

        private SubAgentOptionsBuilder() { }

        public SubAgentOptionsBuilder WithTodoList(TodoListProviderOptions? options = null)
        {
            _todoListOptions = options ?? new();
            return this;
        }

        public SubAgentOptionsBuilder WithFileSystem(FileSystemProviderOptions options)
        {
            _fileSystemOptions = options;
            return this;
        }

        public SubAgentOptionsBuilder WithCompaction(CompactionProviderOptions options)
        {
            _compactionOptions = options;
            return this;
        }

        public SubAgentOptions Build()
        {
            return new SubAgentOptions
            {
                TodoList = _todoListOptions,
                FileSystem = _fileSystemOptions,
                Compaction = _compactionOptions
            };
        }
    }
}
