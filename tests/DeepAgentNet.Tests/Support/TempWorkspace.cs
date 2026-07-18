namespace DeepAgentNet.Tests.Support
{
    internal sealed class TempWorkspace : IDisposable
    {
        public DirectoryInfo Root { get; }

        public TempWorkspace()
        {
            string path = Path.Combine(Path.GetTempPath(), "DeepAgentNet.Tests", Guid.NewGuid().ToString("N"));
            Root = Directory.CreateDirectory(path);
        }

        public string WriteFile(string relativePath, string content)
        {
            string fullPath = Path.Combine(Root.FullName, relativePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (directory is not null)
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        public string ReadFile(string relativePath) =>
            File.ReadAllText(Path.Combine(Root.FullName, relativePath));

        public void Dispose()
        {
            try
            {
                if (Root.Exists)
                    Root.Delete(recursive: true);
            }
            catch
            {
            }
        }
    }
}
