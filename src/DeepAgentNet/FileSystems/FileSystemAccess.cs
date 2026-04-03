using DeepAgentNet.FileSystems.Contracts;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DeepAgentNet.FileSystems
{
    using FileSystemInfo = Contracts.FileSystemInfo;

    public class FileSystemAccess : IFileSystemAccess
    {
        private readonly string _rootPath;
        private readonly FileSystemAccessOptions _options;
        private readonly ILogger<FileSystemAccess>? _logger;

        public FileSystemAccess(string rootPath, FileSystemAccessOptions? options = null, ILoggerFactory? loggerFactory = null)
        {
            _rootPath = rootPath;
            _options = options ?? new FileSystemAccessOptions();
            _logger = loggerFactory?.CreateLogger<FileSystemAccess>();
        }

        private string ResolveFullPath(string path)
        {
            string fullPath = Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(Path.Combine(_rootPath, path));

            if (_options.RestrictToRoot && !fullPath.StartsWith(_rootPath))
            {
                _logger?.AccessOutsideRoot(path);
                throw new UnauthorizedAccessException($"Access to path '{path}' is denied.");
            }

            return fullPath;
        }

        public ValueTask<List<FileSystemInfo>> LsInfoAsync(string path, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(path);
            _logger?.ListingDirectoryInfo(fullPath);

            DirectoryInfo directoryInfo = new(fullPath);

            if (!directoryInfo.Exists)
                return new(new List<FileSystemInfo>());

            List<FileSystemInfo> results = [];

            results.AddRange(directoryInfo.EnumerateFiles().Select(f => new FileSystemInfo(
                Path: f.FullName,
                IsDirectory: false,
                Size: f.Length,
                ModifiedAt: f.LastWriteTime)));

            results.AddRange(directoryInfo.EnumerateDirectories().Select(d => new FileSystemInfo(
                Path: d.FullName + Path.DirectorySeparatorChar,
                IsDirectory: true,
                Size: 0,
                ModifiedAt: d.LastWriteTime)));

            return new(results);
        }

        public async ValueTask<string> ReadAsync(string filePath, int offset = 0, int limit = 500, CancellationToken cancellationToken = default)
        {
            if (limit <= 0)
                return string.Empty;

            string fullPath = ResolveFullPath(filePath);
            _logger?.ReadingFile(fullPath, offset, limit);

            StringBuilder stringBuilder = new();
            var (current, total) = (0, 0);

            await foreach (string line in ReadLineAsync(fullPath, cancellationToken))
            {
                if (current >= offset + limit)
                    break;

                if (current >= offset)
                {
                    WriteFormattedLine(line, current + 1);
                    total++;
                }

                current++;
            }

            if (offset >= current && total == 0 && current > 0)
                throw new IndexOutOfRangeException($"Line offset {offset} exceeds file length ({current} lines)");

            return stringBuilder.ToString();

            void WriteFormattedLine(string line, int lineNum)
            {
                int lineNumberWidth = _options.LineNumberWidth;
                int? maxLineLength = _options.MaxLineLength;

                if (maxLineLength == null || line.Length <= maxLineLength.Value)
                {
                    WriteLine(lineNum.ToString(), line);
                }
                else
                {
                    int numChunks = (int)Math.Ceiling((double)line.Length / maxLineLength.Value);

                    for (int chunkIdx = 0; chunkIdx < numChunks; chunkIdx++)
                    {
                        int start = chunkIdx * maxLineLength.Value;
                        int length = Math.Min(maxLineLength.Value, line.Length - start);
                        string chunk = line.Substring(start, length);

                        WriteLine(chunkIdx == 0 ? lineNum.ToString() : $"{lineNum}.{chunkIdx}", chunk);
                    }
                }

                return;

                void WriteLine(string marker, string content)
                {
                    stringBuilder.Append(marker.PadLeft(lineNumberWidth));
                    stringBuilder.Append('\t');
                    stringBuilder.AppendLine(content);
                }
            }
        }

        public async ValueTask<FileData> ReadRawAsync(string filePath, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(filePath);
            _logger?.ReadingRawFile(fullPath);

            List<string> result = new();

            await foreach (string line in ReadLineAsync(fullPath, cancellationToken))
            {
                result.Add(line);
            }

            FileInfo fileInfo = new(fullPath);

            return new FileData(
                Content: result,
                CreatedAt: fileInfo.CreationTime,
                ModifiedAt: fileInfo.LastWriteTime
            );
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
                async (fileInfo, ct) => await SingleFileGrepAsync(fileInfo, ct));

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
                    await foreach (string line in ReadLineAsync(fileInfo.FullName, ct))
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

            return await task;
        }

        public async ValueTask<WriteResult> WriteAsync(string filePath, string content, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(filePath);
            _logger?.WritingContent(fullPath);

            EnsureDirectory(fullPath);
            await WriteFileAsync(filePath, content, cancellationToken);
            return new WriteResult(filePath);
        }

        public async ValueTask<EditResult> EditAsync(string filePath, string oldString, string newString, bool replaceAll = false, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolveFullPath(filePath);
            _logger?.AttemptingToEditFile(fullPath);

            string content = await ReadFileAsync(cancellationToken, fullPath);

            (string newContent, int occurrences) = PerformStringReplacement(content, oldString, newString, replaceAll);

            await WriteFileAsync(filePath, newContent, cancellationToken);

            return new EditResult(filePath, occurrences);

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
                    throw new ArgumentException($"String not found in file: '{oldString}", nameof(oldString));

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

        private async ValueTask<string> ReadFileAsync(CancellationToken cancellationToken, string fullPath)
        {
            try
            {
                return await File.ReadAllTextAsync(fullPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.FailedToReadFile(ex, fullPath);
                throw;
            }
        }

        private async ValueTask WriteFileAsync(string fullPath, string content, CancellationToken cancellationToken)
        {
            try
            {
                await File.WriteAllTextAsync(fullPath, content, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.FailedToWriteFile(ex, fullPath);
                throw;
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

            await using var _ = stream;
            using StreamReader reader = new(stream);

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
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
