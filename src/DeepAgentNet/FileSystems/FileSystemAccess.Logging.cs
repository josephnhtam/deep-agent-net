using Microsoft.Extensions.Logging;

namespace DeepAgentNet.FileSystems
{
    internal static partial class FileSystemAccessLoggerExtensions
    {
        [LoggerMessage(LogLevel.Warning, "Attempt to access path '{Path}' which is outside of the root path.")]
        public static partial void AccessOutsideRoot(this ILogger<FileSystemAccess> logger, string path);

        [LoggerMessage(LogLevel.Information, "Listing directory info for: {Path}")]
        public static partial void ListingDirectoryInfo(this ILogger<FileSystemAccess> logger, string path);

        [LoggerMessage(LogLevel.Debug, "Reading file {Path} (Offset: {Offset} Limit: {Limit})")]
        public static partial void ReadingFile(this ILogger<FileSystemAccess> logger, string path, int offset, int? limit);

        [LoggerMessage(LogLevel.Debug, "Reading raw file {Path}")]
        public static partial void ReadingRawFile(this ILogger<FileSystemAccess> logger, string path);

        [LoggerMessage(LogLevel.Debug, "Executing grep for pattern '{Pattern}' in {Path} (Glob: {Glob})")]
        public static partial void ExecutingGrep(this ILogger<FileSystemAccess> logger, string pattern, string path, string glob);

        [LoggerMessage(LogLevel.Debug, "Skipping file {FilePath} during grep because it exceeds the maximum allowed size.")]
        public static partial void SkippingFileGrep(this ILogger<FileSystemAccess> logger, string filePath);

        [LoggerMessage(LogLevel.Warning, "Failed to read file {FilePath} during grep.")]
        public static partial void FailedToReadFileGrep(this ILogger<FileSystemAccess> logger, string filePath);

        [LoggerMessage(LogLevel.Warning, "No permission to read file {FilePath} during grep.")]
        public static partial void NoPermissionToReadFileGrep(this ILogger<FileSystemAccess> logger, string filePath);

        [LoggerMessage(LogLevel.Warning, "Failed to process file {FilePath} during grep.")]
        public static partial void FailedToProcessFileGrep(this ILogger<FileSystemAccess> logger, string filePath);

        [LoggerMessage(LogLevel.Debug, "Executing glob '{Pattern}' in {Path}")]
        public static partial void ExecutingGlob(this ILogger<FileSystemAccess> logger, string pattern, string path);

        [LoggerMessage(LogLevel.Debug, "Writing content to {Path}")]
        public static partial void WritingContent(this ILogger<FileSystemAccess> logger, string path);

        [LoggerMessage(LogLevel.Error, "Failed to write to file {FilePath}.")]
        public static partial void FailedToWriteFile(this ILogger<FileSystemAccess> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Debug, "Attempting to overwrite file {Path}")]
        public static partial void AttemptingToOverwriteFile(this ILogger<FileSystemAccess> logger, string path);

        [LoggerMessage(LogLevel.Error, "Failed to overwrite file {FilePath}.")]
        public static partial void FailedToOverwriteFile(this ILogger<FileSystemAccess> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Error, "Failed to edit file {FilePath}.")]
        public static partial void FailedToEditFile(this ILogger<FileSystemAccess> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Debug, "Attempting to edit file {Path}")]
        public static partial void AttemptingToEditFile(this ILogger<FileSystemAccess> logger, string path);

        [LoggerMessage(LogLevel.Error, "Failed to read file {FilePath}.")]
        public static partial void FailedToReadFile(this ILogger<FileSystemAccess> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Error, "Failed to open file stream for {FilePath}.")]
        public static partial void FailedToOpenFileStream(this ILogger<FileSystemAccess> logger, Exception ex, string filePath);

        [LoggerMessage(LogLevel.Error, "Failed to ensure directory {DirectoryPath} exists.")]
        public static partial void FailedToEnsureDirectory(this ILogger<FileSystemAccess> logger, Exception ex, string directoryPath);

        [LoggerMessage(LogLevel.Debug, "Deleting file {Path}")]
        public static partial void DeletingFile(this ILogger<FileSystemAccess> logger, string path);

        [LoggerMessage(LogLevel.Error, "Failed to delete file {FilePath}.")]
        public static partial void FailedToDeleteFile(this ILogger<FileSystemAccess> logger, Exception ex, string filePath);
    }
}
