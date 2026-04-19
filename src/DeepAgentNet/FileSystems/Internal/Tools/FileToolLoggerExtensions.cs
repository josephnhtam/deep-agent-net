using Microsoft.Extensions.Logging;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal static partial class FileToolLoggerExtensions
    {
        [LoggerMessage(LogLevel.Debug, "Reading file {FilePath} (offset={Offset}, limit={Limit})")]
        public static partial void ReadingFile(this ILogger<FileReadToolProvider> logger, string filePath, int offset, int limit);

        [LoggerMessage(LogLevel.Debug, "Read file {FilePath}: {LinesRead} lines read, {TotalLines} total")]
        public static partial void ReadFileCompleted(this ILogger<FileReadToolProvider> logger, string filePath, int linesRead, int totalLines);

        [LoggerMessage(LogLevel.Warning, "Failed to read file {FilePath}")]
        public static partial void ReadFileFailed(this ILogger<FileReadToolProvider> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Debug, "Editing file {FilePath}")]
        public static partial void EditingFile(this ILogger<FileEditToolProvider> logger, string filePath);

        [LoggerMessage(LogLevel.Debug, "Edited file {FilePath}: {Occurrences} occurrence(s) replaced")]
        public static partial void EditFileCompleted(this ILogger<FileEditToolProvider> logger, string filePath, int occurrences);

        [LoggerMessage(LogLevel.Warning, "Failed to edit file {FilePath}")]
        public static partial void EditFileFailed(this ILogger<FileEditToolProvider> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Debug, "Writing file {FilePath}")]
        public static partial void WritingFile(this ILogger<FileWriteToolProvider> logger, string filePath);

        [LoggerMessage(LogLevel.Debug, "Wrote file {FilePath}")]
        public static partial void WriteFileCompleted(this ILogger<FileWriteToolProvider> logger, string filePath);

        [LoggerMessage(LogLevel.Warning, "Failed to write file {FilePath}")]
        public static partial void WriteFileFailed(this ILogger<FileWriteToolProvider> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Debug, "Overwriting file {FilePath}")]
        public static partial void OverwritingFile(this ILogger<FileOverwriteToolProvider> logger, string filePath);

        [LoggerMessage(LogLevel.Debug, "Overwrote file {FilePath}")]
        public static partial void OverwriteFileCompleted(this ILogger<FileOverwriteToolProvider> logger, string filePath);

        [LoggerMessage(LogLevel.Warning, "Failed to overwrite file {FilePath}")]
        public static partial void OverwriteFileFailed(this ILogger<FileOverwriteToolProvider> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Debug, "Deleting file {FilePath}")]
        public static partial void DeletingFile(this ILogger<FileDeleteToolProvider> logger, string filePath);

        [LoggerMessage(LogLevel.Debug, "Deleted file {FilePath}")]
        public static partial void DeleteFileCompleted(this ILogger<FileDeleteToolProvider> logger, string filePath);

        [LoggerMessage(LogLevel.Warning, "Failed to delete file {FilePath}")]
        public static partial void DeleteFileFailed(this ILogger<FileDeleteToolProvider> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Debug, "Reading file data {FilePath}")]
        public static partial void ReadingFileData(this ILogger<FileGetDataToolProvider> logger, string filePath);

        [LoggerMessage(LogLevel.Debug, "Read file data {FilePath}: {Bytes} bytes")]
        public static partial void ReadFileDataCompleted(this ILogger<FileGetDataToolProvider> logger, string filePath, long bytes);

        [LoggerMessage(LogLevel.Warning, "Failed to read file data {FilePath}")]
        public static partial void ReadFileDataFailed(this ILogger<FileGetDataToolProvider> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Debug, "Listing directory {Path} (recursive={Recursive})")]
        public static partial void ListingDirectory(this ILogger<ListInfoToolProvider> logger, string path, bool recursive);

        [LoggerMessage(LogLevel.Debug, "Listed directory {Path}: {EntryCount} entries")]
        public static partial void ListDirectoryCompleted(this ILogger<ListInfoToolProvider> logger, string path, int entryCount);

        [LoggerMessage(LogLevel.Warning, "Failed to list directory {Path}")]
        public static partial void ListDirectoryFailed(this ILogger<ListInfoToolProvider> logger, Exception ex, string path);

        [LoggerMessage(LogLevel.Debug, "Grepping pattern '{Pattern}' in {Path}")]
        public static partial void Grepping(this ILogger<GrepToolProvider> logger, string pattern, string path);

        [LoggerMessage(LogLevel.Debug, "Grep completed for '{Pattern}': {MatchCount} matches")]
        public static partial void GrepCompleted(this ILogger<GrepToolProvider> logger, string pattern, int matchCount);

        [LoggerMessage(LogLevel.Warning, "Grep failed for pattern '{Pattern}'")]
        public static partial void GrepFailed(this ILogger<GrepToolProvider> logger, Exception ex, string pattern);

        [LoggerMessage(LogLevel.Debug, "Globbing pattern '{Pattern}' in {Path}")]
        public static partial void Globbing(this ILogger<GlobToolProvider> logger, string pattern, string path);

        [LoggerMessage(LogLevel.Debug, "Glob completed for '{Pattern}': {MatchCount} matches")]
        public static partial void GlobCompleted(this ILogger<GlobToolProvider> logger, string pattern, int matchCount);

        [LoggerMessage(LogLevel.Warning, "Glob failed for pattern '{Pattern}'")]
        public static partial void GlobFailed(this ILogger<GlobToolProvider> logger, Exception ex, string pattern);
    }
}
