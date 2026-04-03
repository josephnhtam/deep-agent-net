using DeepAgentNet.FileSystems.Contracts;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    public class ListInfoTool
    {
        private readonly IFileSystemAccess _access;

        public ListInfoTool(IFileSystemAccess access)
        {
            _access = access;
        }
    }
}
