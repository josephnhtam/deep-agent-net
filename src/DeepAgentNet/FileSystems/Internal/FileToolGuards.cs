using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared.Internal;
using Microsoft.Agents.AI;

namespace DeepAgentNet.FileSystems.Internal
{
    internal static class FileToolGuards
    {
        public static async ValueTask<string?> ValidateReadStateAsync(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            filePath = PathHelper.NormalizePath(filePath);

            var state = AIAgent.CurrentRunContext?.Session?.StateBag.GetValue<FileReadState>(FileReadState.StateBagKey);

            if (state is null || !state.HasBeenRead(filePath))
                return "Error: File has not been read yet. Use read_file first before editing.";

            DateTime? currentWriteTime = await GetLastWriteTimeUtc(filePath, access, cancellationToken);
            if (currentWriteTime.HasValue && state.IsStale(filePath, currentWriteTime.Value))
                return "Error: File has been modified since it was last read. Read it again before editing.";

            return null;
        }

        public static string? ValidateLsState(string filePath)
        {
            filePath = PathHelper.NormalizePath(filePath);

            var state = AIAgent.CurrentRunContext?.Session?.StateBag.GetValue<LsState>(LsState.StateBagKey);

            int lastSlash = filePath.LastIndexOf('/');
            string parentDir = lastSlash > 0 ? filePath[..lastSlash] : "/";

            if (state is null || !state.HasBeenListed(parentDir))
                return "Error: Parent directory has not been listed yet. Use ls on the parent directory first before creating new files.";

            return null;
        }

        public static async ValueTask UpdateReadStateAsync(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            filePath = PathHelper.NormalizePath(filePath);

            var state = AIAgent.CurrentRunContext?.Session?.StateBag.GetValue<FileReadState>(FileReadState.StateBagKey);

            DateTime lastWriteTime = await GetLastWriteTimeUtc(filePath, access, cancellationToken) ?? DateTime.UtcNow;
            state?.RecordRead(filePath, lastWriteTime);
        }

        public static async ValueTask RecordFileReadAsync(string filePath, IFileSystemAccess access, CancellationToken cancellationToken)
        {
            var session = AIAgent.CurrentRunContext?.Session;
            if (session is null)
                return;

            var state = session.StateBag.GetValue<FileReadState>(FileReadState.StateBagKey);
            if (state is null)
            {
                state = new FileReadState();
                session.StateBag.SetValue(FileReadState.StateBagKey, state);
            }

            string normalized = PathHelper.NormalizePath(filePath);
            DateTime lastWriteTime = await GetLastWriteTimeUtc(normalized, access, cancellationToken) ?? DateTime.UtcNow;
            state.RecordRead(normalized, lastWriteTime);
        }

        private static async ValueTask<DateTime?> GetLastWriteTimeUtc(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            var info = await access.GetInfoAsync(filePath, cancellationToken).ConfigureAwait(false);
            return info?.ModifiedAt.ToUniversalTime();
        }
    }
}
