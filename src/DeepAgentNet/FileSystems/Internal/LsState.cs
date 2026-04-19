using System.Collections.Concurrent;
using DeepAgentNet.Shared.Internal;

namespace DeepAgentNet.FileSystems.Internal
{
    internal class LsState
    {
        public const string StateBagKey = "LsState";

        public ConcurrentDictionary<string, byte> ListedDirs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void Record(string dirPath)
        {
            string key = PathHelper.NormalizePath(dirPath);

            if (string.IsNullOrEmpty(key))
                return;

            ListedDirs[key] = 0;
        }

        public bool HasBeenListed(string dirPath)
        {
            return ListedDirs.ContainsKey(PathHelper.NormalizePath(dirPath));
        }
    }
}
