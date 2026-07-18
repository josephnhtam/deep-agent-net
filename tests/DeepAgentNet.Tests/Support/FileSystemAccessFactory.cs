using DeepAgentNet.FileSystems;

namespace DeepAgentNet.Tests.Support
{
    internal static class FileSystemAccessFactory
    {
        public static FileSystemAccess Create(TempWorkspace workspace, bool restrictToRoot = true) =>
            new(workspace.Root, new FileSystemAccessOptions { RestrictToRoot = restrictToRoot });
    }
}
