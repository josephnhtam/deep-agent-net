using DeepAgentNet.Shared.Internal;

namespace DeepAgentNet.FileSystems.Internal
{
    internal class FileReadState
    {
        public const string StateBagKey = "FileReadState";

        public Dictionary<string, DateTime> ReadFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void RecordRead(string filePath, DateTime fileLastWriteTimeUtc)
        {
            ReadFiles[PathHelper.NormalizePath(filePath)] = fileLastWriteTimeUtc;
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
