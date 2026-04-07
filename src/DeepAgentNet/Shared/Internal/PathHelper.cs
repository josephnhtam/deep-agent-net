namespace DeepAgentNet.Shared.Internal
{
    internal static class PathHelper
    {
        public static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
