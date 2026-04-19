using DeepAgentNet.FileSystems.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.FileSystems.Internal
{
    internal static class FileToolGuards
    {
        internal static readonly ProviderSessionState<FileReadState> FileReadSessionState =
            new(_ => new FileReadState(), FileReadState.StateBagKey, AIJsonUtilities.DefaultOptions);

        internal static readonly ProviderSessionState<LsState> LsSessionState =
            new(_ => new LsState(), LsState.StateBagKey, AIJsonUtilities.DefaultOptions);

        public static async ValueTask<string?> ValidateReadStateAsync(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            filePath = await access.ResolvePathAsync(filePath, cancellationToken: cancellationToken).ConfigureAwait(false);

            var session = AIAgent.CurrentRunContext?.Session;
            if (session is null)
                return null;

            var state = FileReadSessionState.GetOrInitializeState(session);

            if (!state.HasBeenRead(filePath))
                return "Error: File has not been read yet. Use read_file first then retry editing.";

            DateTime? currentWriteTime = await GetLastWriteTimeUtc(filePath, access, cancellationToken);
            if (currentWriteTime.HasValue && state.IsStale(filePath, currentWriteTime.Value))
                return "Error: File has been modified since it was last read. Read it again then retry editing.";

            return null;
        }

        public static async ValueTask<string?> ValidateLsState(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            filePath = await access.ResolvePathAsync(filePath, cancellationToken: cancellationToken).ConfigureAwait(false);

            var session = AIAgent.CurrentRunContext?.Session;
            if (session is null)
                return null;

            var state = LsSessionState.GetOrInitializeState(session);

            string? parentDir = Path.GetDirectoryName(filePath);

            if (parentDir is null || !state.HasBeenListed(parentDir))
                return "Error: Parent directory has not been listed yet. Use ls on the parent directory first then retry creating new files.";

            return null;
        }

        public static async ValueTask UpdateReadStateAsync(string filePath, IFileSystemAccess access, CancellationToken cancellationToken = default)
        {
            var session = AIAgent.CurrentRunContext?.Session;
            if (session is null)
                return;

            filePath = await access.ResolvePathAsync(filePath, cancellationToken: cancellationToken).ConfigureAwait(false);

            var state = FileReadSessionState.GetOrInitializeState(session);
            DateTime lastWriteTime = await GetLastWriteTimeUtc(filePath, access, cancellationToken) ?? DateTime.UtcNow;
            state.RecordRead(filePath, lastWriteTime);
        }

        public static async ValueTask RecordFileReadAsync(string filePath, IFileSystemAccess access, CancellationToken cancellationToken)
        {
            var session = AIAgent.CurrentRunContext?.Session;
            if (session is null)
                return;

            var state = FileReadSessionState.GetOrInitializeState(session);

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
