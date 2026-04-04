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
            string fullPath = Path.GetFullPath(Path.Combine(_rootPath, path.TrimStart('/')));

            if (_options.RestrictToRoot && !fullPath.StartsWith(_rootPath))
            {
                _logger?.AccessOutsideRoot(path);
                throw new UnauthorizedAccessException($"Access to path '{path}' is denied.");
            }

            return fullPath;
        }

        public ValueTask<List<FileSystemInfo>> ListInfoAsync(string path, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(path);
            _logger?.ListingDirectoryInfo(fullPath);

            DirectoryInfo directoryInfo = new(fullPath);

            if (!directoryInfo.Exists)
                return new(new List<FileSystemInfo>());

            List<FileSystemInfo> results = [];

            results.AddRange(directoryInfo.EnumerateDirectories().Select(d => new FileSystemInfo(
                Path: d.FullName + Path.DirectorySeparatorChar,
                IsDirectory: true,
                Size: 0,
                ModifiedAt: d.LastWriteTime)));

            results.AddRange(directoryInfo.EnumerateFiles().Select(f => new FileSystemInfo(
                Path: f.FullName,
                IsDirectory: false,
                Size: f.Length,
                ModifiedAt: f.LastWriteTime)));

            return new(results);
        }

        public async IAsyncEnumerable<string> ReadAsync(string filePath, int offset = 0, int limit = 500, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (limit <= 0)
                yield break;

            string fullPath = ResolveFullPath(filePath);
            _logger?.ReadingFile(fullPath, offset, limit);

            var (lineNumberWidth, maxLineLength) = (_options.LineNumberWidth, _options.MaxLineLength);
            var (current, total) = (0, 0);

            await foreach (string line in ReadLineAsync(fullPath, cancellationToken).ConfigureAwait(false))
            {
                if (current >= offset + limit)
                    break;

                if (current < offset)
                {
                    current++;
                    total++;
                    continue;
                }

                if (maxLineLength == null || line.Length <= maxLineLength.Value)
                {
                    yield return $"{current.ToString().PadLeft(lineNumberWidth)}\t{line}";
                }
                else
                {
                    int numChunks = (int)Math.Ceiling((double)line.Length / maxLineLength.Value);

                    for (int chunkIdx = 0; chunkIdx < numChunks; chunkIdx++)
                    {
                        int start = chunkIdx * maxLineLength.Value;
                        int length = Math.Min(maxLineLength.Value, line.Length - start);
                        string chunk = line.Substring(start, length);
                        string marker = chunkIdx == 0 ? current.ToString() : $"{current}.{chunkIdx}";

                        yield return $"{marker.PadRight(maxLineLength.Value)}\t{chunk}";
                    }
                }

                current++;
                total++;
            }

            if (offset >= current && total == 0 && current > 0)
                throw new IndexOutOfRangeException($"Line offset {offset} exceeds file length ({current} lines)");
        }

        public async ValueTask<List<GrepMatch>> GrepAsync(string pattern, string? dirPath = null, string? glob = null, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(dirPath ?? ".");
            _logger?.ExecutingGrep(pattern, fullPath, glob ?? "*");

            DirectoryInfo directoryInfo = new(fullPath);

            if (!directoryInfo.Exists)
                return [];

            Matcher? matcher = CreateMatcher();
            Regex regex = CreateRegex();

            List<GrepMatch> results = new();

            await Parallel.ForEachAsync(
                directoryInfo.EnumerateFiles(glob ?? "*", SearchOption.AllDirectories),
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

            Regex CreateRegex()
            {
                RegexOptions regexOptions = RegexOptions.Compiled;

                if (_options.NonBacktrackingGrep)
                    regexOptions |= RegexOptions.NonBacktracking;

                return new(pattern, regexOptions);
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

                        if (!regex.IsMatch(line))
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

        public async ValueTask<List<FileSystemInfo>> GlobInfoAsync(string pattern, string? path = null, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(path ?? ".");
            _logger?.ExecutingGlob(pattern, fullPath);

            DirectoryInfo directoryInfo = new(fullPath);

            if (!directoryInfo.Exists)
            {
                return [];
            }

            Matcher matcher = new Matcher().AddInclude(pattern);
            DirectoryInfoWrapper directoryInfoWrapper = new(directoryInfo);
            PatternMatchingResult result = matcher.Execute(directoryInfoWrapper);

            Task<List<FileSystemInfo>> task = Task.Factory.StartNew(() =>
            {
                List<FileSystemInfo> fileSystemInfos = new();

                foreach (var match in result.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string filePath = ResolveFullPath(match.Path);
                    FileInfo fileInfo = new(filePath);

                    fileSystemInfos.Add(new FileSystemInfo(
                        Path: filePath,
                        IsDirectory: false,
                        Size: fileInfo.Length,
                        ModifiedAt: fileInfo.LastWriteTime
                    ));
                }

                return fileSystemInfos;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            return await task.ConfigureAwait(false);
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
            }
        }

        public async ValueTask<EditResult> EditAsync(string filePath, string oldString, string newString, bool replaceAll = false, CancellationToken cancellationToken = default)
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

                int occurrences = content.AsSpan().Count(oldString);

                if (occurrences == 0)
                    throw new ArgumentException($"String not found in file: '{oldString}'", nameof(oldString));

                if (occurrences > 1 && !replaceAll)
                {
                    throw new ArgumentException(
                        $"String '{oldString}' has multiple occurrences (appears {occurrences} times) in file. " +
                        "Use replaceAll=true to replace all instances, or provide a more specific string with surrounding context.",
                        nameof(replaceAll));
                }

                return (content.Replace(oldString, newString), occurrences);
            }
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
        public int LineNumberWidth { get; init; } = 6;
        public int? MaxLineLength { get; init; } = null;
        public bool NonBacktrackingGrep { get; init; } = true;
        public int GrepParallelism { get; init; } = Environment.ProcessorCount;
    }
}
