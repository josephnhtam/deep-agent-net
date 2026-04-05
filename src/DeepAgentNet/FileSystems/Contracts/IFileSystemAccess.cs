namespace DeepAgentNet.FileSystems.Contracts
{
    public interface IFileSystemAccess
    {
        ValueTask<List<FileSystemInfo>> ListInfoAsync(string path, CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> ReadAsync(string filePath, int offset = 0, int limit = 500, CancellationToken cancellationToken = default);
        ValueTask<List<GrepMatch>> GrepAsync(string pattern, string? dirPath = null, string? glob = null, bool isRegex = false, CancellationToken cancellationToken = default);
        ValueTask<List<FileSystemInfo>> GlobInfoAsync(string pattern, string? path = null, CancellationToken cancellationToken = default);
        ValueTask WriteAsync(string filePath, string content, CancellationToken cancellationToken = default);
        ValueTask<EditResult> EditAsync(string filePath, string oldString, string newString, bool replaceAll = false, CancellationToken cancellationToken = default);
        ValueTask DeleteAsync(string filePath, CancellationToken cancellationToken = default);
    }

    public record struct FileSystemInfo(string Path, bool IsDirectory, long Size, DateTime ModifiedAt);
    public record struct GrepMatch(string Path, int Line, string Text);
    public record struct EditResult(int Occurrences);
}
