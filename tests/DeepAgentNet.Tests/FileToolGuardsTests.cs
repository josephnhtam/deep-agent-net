using DeepAgentNet.Agents.Internal;
using DeepAgentNet.FileSystems;
using DeepAgentNet.FileSystems.Internal;
using DeepAgentNet.Tests.Support;
using Microsoft.Extensions.AI;
using System.Text.Json;
using Xunit;

namespace DeepAgentNet.Tests
{
    public class FileToolGuardsTests
    {
        [Fact]
        public async Task ValidateReadState_Should_ReturnError_When_FileWasNotRead()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "content");

            string? error = await FileToolGuards.ValidateReadStateAsync("a.txt", access);

            Assert.NotNull(error);
            Assert.Contains("read_file", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateReadState_Should_ReturnError_When_FileWasModifiedAfterRead()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            string fullPath = workspace.WriteFile("stale.txt", "original");

            await FileToolGuards.RecordFileReadAsync("stale.txt", access, CancellationToken.None);

            await Task.Delay(20);
            File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow.AddSeconds(2));

            string? error = await FileToolGuards.ValidateReadStateAsync("stale.txt", access);

            Assert.NotNull(error);
            Assert.Contains("modified", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateLsState_Should_ReturnError_When_ParentWasNotListed()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("subdir/.keep", "");

            string? error = await FileToolGuards.ValidateLsState("subdir/new.txt", access);

            Assert.NotNull(error);
            Assert.Contains("ls", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateReadState_Should_ReturnNull_When_FileWasRecordedAsRead()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("ok.txt", "content");

            await FileToolGuards.RecordFileReadAsync("ok.txt", access, CancellationToken.None);
            string? error = await FileToolGuards.ValidateReadStateAsync("ok.txt", access);

            Assert.Null(error);
        }

        [Fact]
        public async Task ValidateLsState_Should_ReturnNull_When_ParentWasRecorded()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            Directory.CreateDirectory(Path.Combine(workspace.Root.FullName, "subdir"));

            string parent = await access.ResolvePathAsync("subdir");
            FileToolGuards.LsSessionState.GetOrInitializeState(scope.Session).Record(parent);

            string? error = await FileToolGuards.ValidateLsState("subdir/new.txt", access);

            Assert.Null(error);
        }

        [Fact]
        public async Task UpdateReadStateAsync_Should_ClearStale_When_CalledAfterExternalWrite()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            string fullPath = workspace.WriteFile("f.txt", "original");

            await FileToolGuards.RecordFileReadAsync("f.txt", access, CancellationToken.None);

            await Task.Delay(20);
            File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow.AddSeconds(2));

            await FileToolGuards.UpdateReadStateAsync("f.txt", access);
            string? error = await FileToolGuards.ValidateReadStateAsync("f.txt", access);

            Assert.Null(error);
        }

        [Fact]
        public async Task ValidateReadState_Should_ReturnNull_When_NoSession()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "content");

            string? error = await FileToolGuards.ValidateReadStateAsync("a.txt", access);

            Assert.Null(error);
        }

        [Fact]
        public async Task ValidateLsState_Should_ReturnNull_When_NoSession()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("subdir/.keep", "");

            string? error = await FileToolGuards.ValidateLsState("subdir/new.txt", access);

            Assert.Null(error);
        }
    }

    public class FileSystemPreValidatorTests
    {
        [Fact]
        public async Task PreValidate_Should_Reject_When_EditFileWithoutPriorRead()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "content");

            FunctionCallPreValidValidator validator = new();
            new FileSystemPreValidator(access).Register(validator);

            FunctionCallContent call = new(
                callId: "1",
                name: FileSystemDefaults.EditFileToolName,
                arguments: new Dictionary<string, object?> { ["filePath"] = "a.txt" });

            string? rejection = await validator.PreValidateAsync(call, CancellationToken.None);

            Assert.NotNull(rejection);
            Assert.Contains("read_file", rejection, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PreValidate_Should_Reject_When_WriteFileWithoutPriorLs()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            Directory.CreateDirectory(Path.Combine(workspace.Root.FullName, "subdir"));

            FunctionCallPreValidValidator validator = new();
            new FileSystemPreValidator(access).Register(validator);

            FunctionCallContent call = new(
                callId: "2",
                name: FileSystemDefaults.WriteFileToolName,
                arguments: new Dictionary<string, object?> { ["filePath"] = "subdir/new.txt" });

            string? rejection = await validator.PreValidateAsync(call, CancellationToken.None);

            Assert.NotNull(rejection);
            Assert.Contains("ls", rejection, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PreValidate_Should_Pass_When_ToolIsUnregistered()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);

            FunctionCallPreValidValidator validator = new();
            new FileSystemPreValidator(access).Register(validator);

            FunctionCallContent call = new(
                callId: "3",
                name: "some_unknown_tool",
                arguments: new Dictionary<string, object?>());

            string? rejection = await validator.PreValidateAsync(call, CancellationToken.None);

            Assert.Null(rejection);
        }

        [Fact]
        public async Task PreValidate_Should_Reject_When_DeleteFileWithoutPriorRead()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "content");

            FunctionCallPreValidValidator validator = new();
            new FileSystemPreValidator(access).Register(validator);

            FunctionCallContent call = new(
                callId: "4",
                name: FileSystemDefaults.DeleteFileToolName,
                arguments: new Dictionary<string, object?> { ["filePath"] = "a.txt" });

            string? rejection = await validator.PreValidateAsync(call, CancellationToken.None);

            Assert.NotNull(rejection);
            Assert.Contains("read", rejection, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PreValidate_Should_Reject_When_OverwriteFileWithoutPriorRead()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "content");

            FunctionCallPreValidValidator validator = new();
            new FileSystemPreValidator(access).Register(validator);

            FunctionCallContent call = new(
                callId: "5",
                name: FileSystemDefaults.OverwriteFileToolName,
                arguments: new Dictionary<string, object?> { ["filePath"] = "a.txt" });

            string? rejection = await validator.PreValidateAsync(call, CancellationToken.None);

            Assert.NotNull(rejection);
            Assert.Contains("read", rejection, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PreValidate_Should_Pass_When_EditFileAfterRecordedRead()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "content");

            await FileToolGuards.RecordFileReadAsync("a.txt", access, CancellationToken.None);

            FunctionCallPreValidValidator validator = new();
            new FileSystemPreValidator(access).Register(validator);

            FunctionCallContent call = new(
                callId: "6",
                name: FileSystemDefaults.EditFileToolName,
                arguments: new Dictionary<string, object?> { ["filePath"] = "a.txt" });

            string? rejection = await validator.PreValidateAsync(call, CancellationToken.None);

            Assert.Null(rejection);
        }

        [Fact]
        public async Task PreValidate_Should_Pass_When_WriteFileAfterParentListed()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            Directory.CreateDirectory(Path.Combine(workspace.Root.FullName, "subdir"));

            string parent = await access.ResolvePathAsync("subdir");
            FileToolGuards.LsSessionState.GetOrInitializeState(scope.Session).Record(parent);

            FunctionCallPreValidValidator validator = new();
            new FileSystemPreValidator(access).Register(validator);

            FunctionCallContent call = new(
                callId: "7",
                name: FileSystemDefaults.WriteFileToolName,
                arguments: new Dictionary<string, object?> { ["filePath"] = "subdir/new.txt" });

            string? rejection = await validator.PreValidateAsync(call, CancellationToken.None);

            Assert.Null(rejection);
        }

        [Fact]
        public async Task PreValidate_Should_ThrowArgumentException_When_FilePathMissing()
        {
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);

            FunctionCallPreValidValidator validator = new();
            new FileSystemPreValidator(access).Register(validator);

            FunctionCallContent call = new(
                callId: "8",
                name: FileSystemDefaults.EditFileToolName,
                arguments: new Dictionary<string, object?>());

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await validator.PreValidateAsync(call, CancellationToken.None));
        }

        [Fact]
        public async Task PreValidate_Should_Reject_When_FilePathIsJsonElementWithoutPriorRead()
        {
            var (agent, session) = await GuardSessionScope.CreateSessionAsync();
            await using GuardSessionScope scope = GuardSessionScope.Enter(agent, session);
            using TempWorkspace workspace = new();
            FileSystemAccess access = FileSystemAccessFactory.Create(workspace);
            workspace.WriteFile("a.txt", "content");

            FunctionCallPreValidValidator validator = new();
            new FileSystemPreValidator(access).Register(validator);

            FunctionCallContent call = new(
                callId: "9",
                name: FileSystemDefaults.EditFileToolName,
                arguments: new Dictionary<string, object?>
                {
                    ["filePath"] = JsonSerializer.SerializeToElement("a.txt")
                });

            string? rejection = await validator.PreValidateAsync(call, CancellationToken.None);

            Assert.NotNull(rejection);
            Assert.Contains("read_file", rejection, StringComparison.OrdinalIgnoreCase);
        }
    }
}
