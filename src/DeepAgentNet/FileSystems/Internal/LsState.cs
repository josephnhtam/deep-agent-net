namespace DeepAgentNet.FileSystems.Internal
{
    internal class LsState
    {
        public const string StateBagKey = "LsState";

        public HashSet<string> ListedDirs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void Record(string dirPath)
        {
            ListedDirs.Add(Normalize(dirPath));
        }

        public bool HasBeenListed(string dirPath)
        {
            return ListedDirs.Contains(Normalize(dirPath));
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
