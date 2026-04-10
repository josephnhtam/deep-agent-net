using DeepAgentNet.FileSystems.Contracts;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace DeepAgentNet.FileSystems
{
    using FileSystemInfo = Contracts.FileSystemInfo;

    public class FileSystemAccess : IFileSystemAccess
    {
        private readonly string _rootPath;
        private readonly FileSystemAccessOptions _options;
        private readonly ILogger<FileSystemAccess>? _logger;

        public FileSystemAccess(DirectoryInfo root, FileSystemAccessOptions? options = null, ILoggerFactory? loggerFactory = null)
        {
            _rootPath = root.FullName;
            _options = options ?? new FileSystemAccessOptions();
            _logger = loggerFactory?.CreateLogger<FileSystemAccess>();
        }

        private string ResolveFullPath(string path)
        {
            string fullPath = Path.GetFullPath(Path.Combine(_rootPath, path.Replace('\\', '/').TrimStart('/')));

            if (_options.RestrictToRoot && !fullPath.StartsWith(_rootPath))
            {
                _logger?.AccessOutsideRoot(path);
                throw new UnauthorizedAccessException($"Access to path '{path}' is denied.");
            }

            return fullPath;
        }

        public async IAsyncEnumerable<FileSystemInfo> ListInfoAsync(
            string path, bool recursive = false, string[]? ignore = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(path);
            _logger?.ListingDirectoryInfo(fullPath);

            DirectoryInfo directoryInfo = new(fullPath);

            if (!directoryInfo.Exists)
                yield break;

            var ignorePatterns = ignore is { Length: > 0 }
                ? _options.LsIgnorePatterns.Concat(ignore).ToArray()
                : _options.LsIgnorePatterns;

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            int count = 0;

            foreach (var entry in directoryInfo.EnumerateDirectories("*", searchOption))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relative = Path.GetRelativePath(fullPath, entry.FullName).Replace('\\', '/');

                if (IsIgnored(relative, ignorePatterns))
                    continue;

                yield return new FileSystemInfo(
                    Path: $"{relative}/",
                    IsDirectory: true,
                    Size: 0,
                    ModifiedAt: entry.LastWriteTime);

                if (++count % 100 == 0)
                    await Task.Yield();
            }

            foreach (var entry in directoryInfo.EnumerateFiles("*", searchOption))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relative = Path.GetRelativePath(fullPath, entry.FullName).Replace('\\', '/');

                if (IsIgnored(relative, ignorePatterns))
                    continue;

                yield return new FileSystemInfo(
                    Path: relative,
                    IsDirectory: false,
                    Size: entry.Length,
                    ModifiedAt: entry.LastWriteTime);

                if (++count % 100 == 0)
                    await Task.Yield();
            }

            static bool IsIgnored(string relativePath, string[] patterns)
            {
                foreach (string pattern in patterns)
                {
                    if (relativePath.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                        relativePath.Contains("/" + pattern, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
        }

        public async IAsyncEnumerable<string> ReadAsync(
            string filePath, int offset = 0, int? limit = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (limit <= 0)
                yield break;

            string fullPath = ResolveFullPath(filePath);
            _logger?.ReadingFile(fullPath, offset, limit);

            var (current, total) = (0, 0);
            await foreach (string line in ReadLineAsync(fullPath, cancellationToken).ConfigureAwait(false))
            {
                if (limit.HasValue && current >= offset + limit)
                    break;

                if (current < offset)
                {
                    current++;
                    total++;
                    continue;
                }

                yield return line;

                current++;
                total++;
            }

            if (offset >= current && total == 0 && current > 0)
                throw new IndexOutOfRangeException($"Line offset {offset} exceeds file length ({current} lines)");
        }

        public async ValueTask<List<GrepMatch>> GrepAsync(
            string pattern, string? dirPath = null, string? glob = null, bool isRegex = false, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(dirPath ?? ".");
            _logger?.ExecutingGrep(pattern, fullPath, glob ?? "*");

            DirectoryInfo directoryInfo = new(fullPath);

            if (!directoryInfo.Exists)
                return [];

            Regex? regex = isRegex ? new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;

            Matcher? matcher = CreateMatcher();

            List<GrepMatch> results = new();

            await Parallel.ForEachAsync(
                directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories),
                new ParallelOptions { MaxDegreeOfParallelism = _options.GrepParallelism },
                async (fileInfo, ct) => await SingleFileGrepAsync(fileInfo, ct).ConfigureAwait(false)
            ).ConfigureAwait(false);

            return results;

            Matcher? CreateMatcher()
            {
                if (string.IsNullOrWhiteSpace(glob))
                    return null;

                return new Matcher().AddInclude(glob);
            }

            async ValueTask SingleFileGrepAsync(FileInfo fileInfo, CancellationToken ct)
            {
                try
                {
                    string relativePath = Path.GetRelativePath(fullPath, fileInfo.FullName).Replace('\\', '/');

                    if (matcher != null && !matcher.Match(relativePath).HasMatches)
                        return;

                    if (fileInfo.Length > _options.MaxGrepFileBytesSize)
                    {
                        _logger?.SkippingFileGrep(fileInfo.FullName);
                        return;
                    }

                    int lineNumber = 0;
                    await foreach (string line in ReadLineAsync(fileInfo.FullName, ct).ConfigureAwait(false))
                    {
                        lineNumber++;

                        bool isMatch = regex?.IsMatch(line) ?? line.Contains(pattern, StringComparison.OrdinalIgnoreCase);

                        if (!isMatch)
                            continue;

                        lock (results)
                        {
                            results.Add(new GrepMatch(relativePath, lineNumber, line));
                        }
                    }
                }
                catch (IOException)
                {
                    _logger?.FailedToReadFileGrep(fileInfo.FullName);
                }
                catch (UnauthorizedAccessException)
                {
                    _logger?.NoPermissionToReadFileGrep(fileInfo.FullName);
                }
                catch (Exception)
                {
                    _logger?.FailedToProcessFileGrep(fileInfo.FullName);
                }
            }
        }

        public async IAsyncEnumerable<FileSystemInfo> GlobInfoAsync(
            string pattern, string? path = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(path ?? ".");
            _logger?.ExecutingGlob(pattern, fullPath);

            DirectoryInfo directoryInfo = new(fullPath);

            if (!directoryInfo.Exists)
                yield break;

            Matcher matcher = new Matcher().AddInclude(pattern);
            DirectoryInfoWrapper directoryInfoWrapper = new(directoryInfo);
            PatternMatchingResult result = matcher.Execute(directoryInfoWrapper);

            int count = 0;
            foreach (var match in result.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileInfo fileInfo = new(Path.Combine(fullPath, match.Path));

                yield return new FileSystemInfo(
                    Path: match.Path,
                    IsDirectory: false,
                    Size: fileInfo.Length,
                    ModifiedAt: fileInfo.LastWriteTime);

                if (++count % 100 == 0)
                    await Task.Yield();
            }
        }

        public async ValueTask WriteAsync(string filePath, string content, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(filePath);
            _logger?.WritingContent(fullPath);

            FileInfo fileInfo = new(fullPath);
            bool isSymlink = fileInfo.Exists && fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);

            if (isSymlink)
                throw new IOException($"Cannot write to {filePath} because it is a symlink. Symlinks are not allowed.");

            if (fileInfo.Exists)
                throw new IOException($"Cannot write to {filePath} because it already exists.");

            EnsureDirectory(fullPath);

            try
            {
                var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using var _ = fileStream.ConfigureAwait(false);

                var writer = new StreamWriter(fileStream);
                await using var __ = writer.ConfigureAwait(false);

                await writer.WriteAsync(content).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.FailedToWriteFile(ex, fullPath);
                throw;
            }
        }

        public async ValueTask OverwriteAsync(string filePath, string content, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(filePath);
            _logger?.AttemptingToOverwriteFile(fullPath);

            try
            {
                FileInfo fileInfo = new(fullPath);

                if (!fileInfo.Exists)
                    throw new FileNotFoundException("File not found");

                if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException("Symlinks are not allowed");

                var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Write, FileShare.None);
                await using var _ = fileStream.ConfigureAwait(false);

                fileStream.SetLength(0);

                var writer = new StreamWriter(fileStream);
                await using var __ = writer.ConfigureAwait(false);

                await writer.WriteAsync(content).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.FailedToOverwriteFile(ex, fullPath);
                throw;
            }
        }

        public async ValueTask<EditResult> EditAsync(
            string filePath, string oldString, string newString, bool replaceAll = false, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(filePath);
            _logger?.AttemptingToEditFile(fullPath);

            try
            {
                FileInfo fileInfo = new(fullPath);

                if (!fileInfo.Exists)
                    throw new FileNotFoundException("File not found");

                if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException("Symlinks are not allowed");

                var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                await using var _ = fileStream.ConfigureAwait(false);

                using var reader = new StreamReader(fileStream, leaveOpen: true);
                string content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                (string newContent, int occurrences) = PerformStringReplacement(content, oldString, newString, replaceAll);

                fileStream.Position = 0;
                fileStream.SetLength(0);

                var writer = new StreamWriter(fileStream, leaveOpen: true);
                await using var __ = writer.ConfigureAwait(false);

                await writer.WriteAsync(newContent.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                return new EditResult(occurrences);
            }
            catch (Exception ex)
            {
                _logger?.FailedToEditFile(ex, fullPath);
                throw;
            }

            static (string NewContent, int Occurrences) PerformStringReplacement(string content, string oldString, string newString, bool replaceAll)
            {
                if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(oldString))
                {
                    return (newString, 0);
                }

                if (string.IsNullOrEmpty(oldString))
                    throw new ArgumentException("OldString cannot be empty when file has content", nameof(oldString));

                bool hasCRLF = content.Contains("\r\n");
                string normalized = hasCRLF ? content.Replace("\r\n", "\n") : content;
                string normalizedOld = hasCRLF ? oldString.Replace("\r\n", "\n") : oldString;
                string normalizedNew = hasCRLF ? newString.Replace("\r\n", "\n") : newString;

                int occurrences = normalized.AsSpan().Count(normalizedOld);

                if (occurrences == 0)
                {
                    string truncated = oldString.Length > 200 ? oldString[..200] + "..." : oldString;
                    throw new ArgumentException($"String to replace not found in file.\nString: {truncated}", nameof(oldString));
                }

                if (occurrences > 1 && !replaceAll)
                {
                    string truncated = oldString.Length > 200 ? oldString[..200] + "..." : oldString;
                    throw new ArgumentException(
                        $"Found {occurrences} matches of the string to replace, but replaceAll is false. " +
                        "To replace all occurrences, set replaceAll to true. " +
                        $"To replace only one occurrence, please provide more context to uniquely identify the instance.\nString: {truncated}",
                        nameof(replaceAll));
                }

                bool stripTrailingNewline = string.IsNullOrEmpty(normalizedNew)
                    && !normalizedOld.EndsWith('\n')
                    && normalized.Contains(normalizedOld + "\n");

                string searchString = stripTrailingNewline ? normalizedOld + "\n" : normalizedOld;
                string result = normalized.Replace(searchString, normalizedNew);

                if (hasCRLF)
                    result = result.Replace("\n", "\r\n");

                return (result, occurrences);
            }
        }

        public ValueTask DeleteAsync(string filePath, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(filePath);
            _logger?.DeletingFile(fullPath);

            try
            {
                FileInfo fileInfo = new(fullPath);

                if (!fileInfo.Exists)
                    throw new FileNotFoundException("File not found", fullPath);

                if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException($"Cannot delete {filePath} because it is a symlink. Symlinks are not allowed.");

                File.Delete(fullPath);
                return default;
            }
            catch (Exception ex)
            {
                _logger?.FailedToDeleteFile(ex, fullPath);
                throw;
            }
        }

        public ValueTask<FileSystemInfo?> GetInfoAsync(string filePath, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(filePath);
            FileInfo fileInfo = new(fullPath);

            if (!fileInfo.Exists)
                return new((FileSystemInfo?)null);

            string relative = Path.GetRelativePath(fullPath, fileInfo.FullName).Replace('\\', '/');

            return new(new FileSystemInfo(
                Path: relative,
                IsDirectory: false,
                Size: fileInfo.Length,
                ModifiedAt: fileInfo.LastWriteTime));
        }

        private async IAsyncEnumerable<string> ReadLineAsync(string fullPath, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            FileInfo fileInfo = new(fullPath);

            if (!fileInfo.Exists)
                throw new FileNotFoundException("File not found");

            if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Symlinks are not allowed");

            if (fileInfo.Length == 0)
                yield break;

            FileStream stream;

            try
            {
                stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            }
            catch (Exception ex)
            {
                _logger?.FailedToOpenFileStream(ex, fullPath);
                throw;
            }

            await using var _ = stream.ConfigureAwait(false);
            using StreamReader reader = new(stream);

            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                yield return line;
            }
        }

        private void EnsureDirectory(string fullPath)
        {
            try
            {
                string? dirName = Path.GetDirectoryName(fullPath);

                if (!string.IsNullOrEmpty(dirName))
                {
                    DirectoryInfo directoryInfo = new(dirName);

                    if (!directoryInfo.Exists)
                        directoryInfo.Create();
                }
            }
            catch (Exception ex)
            {
                _logger?.FailedToEnsureDirectory(ex, fullPath);
                throw;
            }
        }
    }

    public record FileSystemAccessOptions
    {
        public bool RestrictToRoot { get; init; } = true;
        public long MaxGrepFileBytesSize { get; init; } = 10_000_000;
        public int GrepParallelism { get; init; } = Environment.ProcessorCount;
        public string[] LsIgnorePatterns { get; init; } =
        [
            "node_modules/", "__pycache__/", ".git/", "dist/", "build/",
            "target/", "vendor/", "bin/", "obj/", ".idea/", ".vscode/",
            ".zig-cache/", "zig-out/", ".coverage/", "coverage/",
            "tmp/", "temp/", ".cache/", "cache/", "logs/", ".venv/", "venv/", "env/"
        ];
    }
}
