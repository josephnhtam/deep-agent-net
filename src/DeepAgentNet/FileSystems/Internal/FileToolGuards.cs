using DeepAgentNet.FileSystems.Contracts;
using Microsoft.Agents.AI;

namespace DeepAgentNet.FileSystems.Internal
{
    internal static class FileToolGuards
    {
        public static async ValueTask<string?> ValidateReadStateAsync(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            filePath = await access.ResolvePathAsync(filePath, cancellationToken: cancellationToken).ConfigureAwait(false);

            var state = AIAgent.CurrentRunContext?.Session?.StateBag.GetValue<FileReadState>(FileReadState.StateBagKey);

            if (state is null || !state.HasBeenRead(filePath))
                return "Error: File has not been read yet. Use read_file first then retry editing.";

            DateTime? currentWriteTime = await GetLastWriteTimeUtc(filePath, access, cancellationToken);
            if (currentWriteTime.HasValue && state.IsStale(filePath, currentWriteTime.Value))
                return "Error: File has been modified since it was last read. Read it again then retry editing.";

            return null;
        }

        public static async ValueTask<string?> ValidateLsState(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            filePath = await access.ResolvePathAsync(filePath, cancellationToken: cancellationToken).ConfigureAwait(false);

            var state = AIAgent.CurrentRunContext?.Session?.StateBag.GetValue<LsState>(LsState.StateBagKey);

            string? parentDir = Path.GetDirectoryName(filePath);

            if (parentDir is null || state is null || !state.HasBeenListed(parentDir))
                return "Error: Parent directory has not been listed yet. Use ls on the parent directory first then retry creating new files.";

            return null;
        }

        public static async ValueTask UpdateReadStateAsync(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            filePath = await access.ResolvePathAsync(filePath, cancellationToken: cancellationToken).ConfigureAwait(false);

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

            filePath = await access.ResolvePathAsync(filePath, cancellationToken: cancellationToken).ConfigureAwait(false);
            DateTime lastWriteTime = await GetLastWriteTimeUtc(filePath, access, cancellationToken) ?? DateTime.UtcNow;
            state.RecordRead(filePath, lastWriteTime);
        }

        private static async ValueTask<DateTime?> GetLastWriteTimeUtc(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            var info = await access.GetInfoAsync(filePath, cancellationToken).ConfigureAwait(false);
            return info?.ModifiedAt.ToUniversalTime();
        }
    }
}
