namespace DeepAgentNet.FileSystems.Internal
{
    internal class FileReadState
    {
        public const string StateBagKey = "FileReadState";

        public Dictionary<string, DateTime> ReadFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void RecordRead(string filePath, DateTime fileLastWriteTimeUtc)
        {
            ReadFiles[filePath] = fileLastWriteTimeUtc;
        }

        public bool HasBeenRead(string filePath)
        {
            return ReadFiles.ContainsKey(filePath);
        }

        public bool IsStale(string filePath, DateTime currentLastWriteTimeUtc)
        {
            return ReadFiles.TryGetValue(filePath, out DateTime recorded) && currentLastWriteTimeUtc > recorded;
        }
    }
}
