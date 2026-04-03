namespace DeepAgentNet.FileSystems.Contracts
{
    public interface IFileSystemAccess
    {
        ValueTask<List<FileSystemInfo>> ListInfoAsync(string path, CancellationToken cancellationToken = default);
        ValueTask<string> ReadAsync(string filePath, int offset = 0, int limit = 500, CancellationToken cancellationToken = default);
        ValueTask<FileData> ReadRawAsync(string filePath, CancellationToken cancellationToken = default);
        ValueTask<List<GrepMatch>> GrepAsync(string pattern, string? dirPath = null, string? glob = null, CancellationToken cancellationToken = default);
        ValueTask<List<FileSystemInfo>> GlobInfoAsync(string pattern, string? path = null, CancellationToken cancellationToken = default);
        ValueTask<WriteResult> WriteAsync(string filePath, string content, CancellationToken cancellationToken = default);
        ValueTask<EditResult> EditAsync(string filePath, string oldString, string newString, bool replaceAll = false, CancellationToken cancellationToken = default);
    }

    public record struct FileSystemInfo(string Path, bool IsDirectory, long Size, DateTime ModifiedAt);
    public record struct FileData(List<string> Content, DateTime CreatedAt, DateTime ModifiedAt);
    public record struct GrepMatch(string Path, int Line, string Text);
    public record struct WriteResult(string Path, Dictionary<string, string>? Metadata = null);
    public record struct EditResult(string Path, int Occurrences, Dictionary<string, string>? Metadata = null);
}
