using System.Collections.Concurrent;
using DeepAgentNet.Shared.Internal;

namespace DeepAgentNet.FileSystems.Internal
{
    internal class FileReadState
    {
        public const string StateBagKey = "FileReadState";

        public ConcurrentDictionary<string, DateTime> ReadFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void RecordRead(string filePath, DateTime fileLastWriteTimeUtc)
        {
            string key = PathHelper.NormalizePath(filePath);

            if (string.IsNullOrEmpty(key))
                return;

            ReadFiles[key] = fileLastWriteTimeUtc;
        }

        public bool HasBeenRead(string filePath)
        {
            return ReadFiles.ContainsKey(PathHelper.NormalizePath(filePath));
        }

        public bool IsStale(string filePath, DateTime currentLastWriteTimeUtc)
        {
            return ReadFiles.TryGetValue(PathHelper.NormalizePath(filePath), out DateTime recorded) && currentLastWriteTimeUtc > recorded;
        }
    }
}
