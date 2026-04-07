using DeepAgentNet.Shared.Internal;

namespace DeepAgentNet.FileSystems.Internal
{
    internal class LsState
    {
        public const string StateBagKey = "LsState";

        public HashSet<string> ListedDirs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void Record(string dirPath)
        {
            ListedDirs.Add(PathHelper.NormalizePath(dirPath));
        }

        public bool HasBeenListed(string dirPath)
        {
            return ListedDirs.Contains(PathHelper.NormalizePath(dirPath));
        }
    }
}
