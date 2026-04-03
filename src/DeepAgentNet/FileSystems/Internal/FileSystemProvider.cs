using DeepAgentNet.FileSystems.Contracts;
using Microsoft.Agents.AI;

namespace DeepAgentNet.FileSystems.Internal
{
    internal class FileSystemProvider : AIContextProvider
    {
        private readonly IFileSystemAccess _access;

        public FileSystemProvider(IFileSystemAccess access)
        {
            _access = access;
        }


    }
}
