using DeepAgentNet.FileSystems;
using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Tests.Support;
using Xunit;
using FsInfo = DeepAgentNet.FileSystems.Contracts.FileSystemInfo;

namespace DeepAgentNet.Tests
{
    public class FileSystemAccessTests
    {
        [Fact]
        public async Task ResolvePathAsync_Should_ThrowUnauthorizedAccessException_When_PathIsOutsideRoot()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await access.ResolvePathAsync("../outside-the-workspace"));
        }

        [Fact]
        public async Task EditAsync_Should_ReplaceSingleMatch_When_OldStringOccursOnce()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("hello.txt", "hello world");

            EditResult result = await access.EditAsync("hello.txt", "world", "there");

            Assert.Equal(1, result.Occurrences);
            Assert.Equal("hello there", workspace.ReadFile("hello.txt"));
        }

        [Fact]
        public async Task EditAsync_Should_ReplaceAllOccurrences_When_ReplaceAllIsTrue()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "a a a");

            EditResult result = await access.EditAsync("a.txt", "a", "b", replaceAll: true);

            Assert.Equal(3, result.Occurrences);
            Assert.Equal("b b b", workspace.ReadFile("a.txt"));
        }

        [Fact]
        public async Task EditAsync_Should_PreserveCrlf_When_FileUsesCrlfLineEndings()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("crlf.txt", "line1\r\nold\r\nline3\r\n");

            await access.EditAsync("crlf.txt", "old", "new");

            string content = workspace.ReadFile("crlf.txt");
            Assert.Contains("\r\n", content);
            Assert.Equal("line1\r\nnew\r\nline3\r\n", content);
        }

        [Fact]
        public async Task EditAsync_Should_ThrowArgumentException_When_OldStringIsAbsent()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("file.txt", "hello world");

            ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await access.EditAsync("file.txt", "missing-snippet", "replacement"));

            Assert.Contains("missing-snippet", ex.Message);
        }

        [Fact]
        public async Task EditAsync_Should_ThrowArgumentException_When_MultipleMatchesWithoutReplaceAll()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "a a a");

            ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await access.EditAsync("a.txt", "a", "b", replaceAll: false));

            Assert.Contains("replaceAll", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GrepAsync_Should_ReturnMatches_When_SearchingLiteralPattern()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("one.txt", "alpha\nfindme\nbeta");
            workspace.WriteFile("two.txt", "nothing here");
            workspace.WriteFile("three.txt", "also findme here");

            List<GrepMatch> matches = await access.GrepAsync("findme");

            Assert.Equal(2, matches.Count);
            Assert.Contains(matches, m => m.Path.Replace('\\', '/').EndsWith("one.txt") && m.Line == 2);
            Assert.Contains(matches, m => m.Path.Replace('\\', '/').EndsWith("three.txt"));
        }

        [Fact]
        public async Task GrepAsync_Should_ReturnMatches_When_SearchingRegexPattern()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("code.cs", "int foo = 1;\nint bar = 2;");

            List<GrepMatch> matches = await access.GrepAsync(@"int\s+\w+\s*=", isRegex: true);

            Assert.Equal(2, matches.Count);
            Assert.All(matches, m => Assert.Contains("int ", m.Text));
        }

        [Fact]
        public async Task GrepAsync_Should_FilterByGlob_When_GlobProvided()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "needle");
            workspace.WriteFile("a.cs", "needle");

            List<GrepMatch> matches = await access.GrepAsync("needle", glob: "*.txt");

            Assert.Single(matches);
            Assert.EndsWith("a.txt", matches[0].Path.Replace('\\', '/'));
        }

        [Fact]
        public async Task GrepAsync_Should_ReturnEmpty_When_DirectoryMissing()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);

            List<GrepMatch> matches = await access.GrepAsync("needle", dirPath: "does-not-exist");

            Assert.Empty(matches);
        }

        [Fact]
        public async Task GrepAsync_Should_SkipFile_When_ExceedsMaxGrepFileBytesSize()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = new(
                workspace.Root,
                new FileSystemAccessOptions
                {
                    RestrictToRoot = true,
                    MaxGrepFileBytesSize = 10
                });
            workspace.WriteFile("big.txt", "0123456789abcdefghij-needle");

            List<GrepMatch> matches = await access.GrepAsync("needle");

            Assert.Empty(matches);
        }

        [Fact]
        public async Task GlobInfoAsync_Should_ReturnOnlyInRootMatches_When_PatternMatchesFiles()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("src/App.cs", "class App {}");
            workspace.WriteFile("src/Util.cs", "class Util {}");
            workspace.WriteFile("readme.md", "# readme");

            List<FsInfo> matches = await CollectAsync(access.GlobInfoAsync("**/*.cs"));

            Assert.Equal(2, matches.Count);
            Assert.All(matches, m => Assert.EndsWith(".cs", m.Path.Replace('\\', '/')));
            Assert.DoesNotContain(matches, m => m.Path.Contains("readme", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ListInfoAsync_Should_ReturnTopLevelEntries_When_DirectoryHasFiles()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "a");
            workspace.WriteFile("subdir/.keep", "");
            workspace.WriteFile("node_modules/pkg/x.txt", "ignored");

            List<FsInfo> entries = await CollectAsync(access.ListInfoAsync(".", recursive: true));

            Assert.Contains(entries, e => e.Path == "a.txt" && !e.IsDirectory);
            Assert.Contains(entries, e => e.Path == "subdir/" && e.IsDirectory);
            Assert.DoesNotContain(entries, e =>
                e.Path.Replace('\\', '/').Contains("node_modules/pkg", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ListInfoAsync_Should_RespectCustomIgnore_When_RecursiveIsTrue()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("deep/nested.txt", "ok");
            workspace.WriteFile("skip/hidden.txt", "no");

            List<FsInfo> entries = await CollectAsync(
                access.ListInfoAsync(".", recursive: true, ignore: ["skip/"]));

            Assert.Contains(entries, e => e.Path.Replace('\\', '/').Contains("deep/nested.txt"));
            Assert.DoesNotContain(entries, e => e.Path.Replace('\\', '/').Contains("skip/hidden"));
        }

        [Fact]
        public async Task ReadAsync_Should_ReturnLineSlice_When_OffsetAndLimitProvided()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("lines.txt", "l0\nl1\nl2\nl3\nl4\n");

            List<string> lines = await CollectAsync(access.ReadAsync("lines.txt", offset: 1, limit: 2));

            Assert.Equal(["l1", "l2"], lines);
        }

        [Fact]
        public async Task ReadAsync_Should_YieldEmpty_When_OffsetBeyondFile()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("short.txt", "a\nb\n");

            List<string> lines = await CollectAsync(access.ReadAsync("short.txt", offset: 5));

            Assert.Empty(lines);
        }

        [Fact]
        public async Task ReadAsync_Should_YieldEmpty_When_LimitIsNonPositive()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("lines.txt", "a\nb\n");

            List<string> lines = await CollectAsync(access.ReadAsync("lines.txt", limit: 0));

            Assert.Empty(lines);
        }

        [Fact]
        public async Task WriteAsync_Should_CreateNestedFile_When_ParentDirectoryMissing()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);

            await access.WriteAsync("new/nested.txt", "data");

            Assert.Equal("data", workspace.ReadFile("new/nested.txt"));
        }

        [Fact]
        public async Task WriteAsync_Should_ThrowIOException_When_FileAlreadyExists()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("exists.txt", "old");

            IOException ex = await Assert.ThrowsAsync<IOException>(async () =>
                await access.WriteAsync("exists.txt", "new"));

            Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task WriteAsync_Should_WriteBytes_When_StreamOverloadUsed()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            byte[] bytes = [1, 2, 3];

            await access.WriteAsync("bin.dat", async stream =>
            {
                await stream.WriteAsync(bytes);
            });

            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(workspace.Root.FullName, "bin.dat")));
        }

        [Fact]
        public async Task OverwriteAsync_Should_ReplaceContent_When_FileExists()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("f.txt", "old");

            await access.OverwriteAsync("f.txt", "new");

            Assert.Equal("new", workspace.ReadFile("f.txt"));
        }

        [Fact]
        public async Task DeleteAsync_Should_RemoveFile_When_FileExists()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("gone.txt", "x");

            await access.DeleteAsync("gone.txt");

            Assert.False(File.Exists(Path.Combine(workspace.Root.FullName, "gone.txt")));
        }

        [Fact]
        public async Task GetInfoAsync_Should_ReturnMetadata_When_FileExists()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("meta.txt", "abc");

            FsInfo? info = await access.GetInfoAsync("meta.txt");

            Assert.True(info.HasValue);
            Assert.Equal(3, info.Value.Size);
            Assert.False(info.Value.IsDirectory);
        }

        [Fact]
        public async Task GetInfoAsync_Should_ReturnNull_When_FileMissing()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);

            FsInfo? info = await access.GetInfoAsync("missing.txt");

            Assert.Null(info);
        }

        private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
        {
            List<T> items = [];
            await foreach (T item in source)
                items.Add(item);
            return items;
        }
    }
}
