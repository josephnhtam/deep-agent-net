using DeepAgentNet.Agents.Internal;
using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared.Internal;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.FileSystems.Internal
{
    internal static class FileToolGuards
    {
        public static string? ValidateReadState(string filePath, IFileSystemAccess access)
        {
            filePath = PathHelper.NormalizePath(filePath);

            var session = FunctionInvokingChatClient.CurrentContext?.Options?.GetSession();
            var state = session?.StateBag.GetValue<FileReadState>(FileReadState.StateBagKey);

            if (state is null || !state.HasBeenRead(filePath))
                return "Error: File has not been read yet. Use read_file first before editing.";

            DateTime? currentWriteTime = access.GetLastWriteTimeUtc(filePath);
            if (currentWriteTime.HasValue && state.IsStale(filePath, currentWriteTime.Value))
                return "Error: File has been modified since it was last read. Read it again before editing.";

            return null;
        }

        public static string? ValidateLsState(string filePath)
        {
            filePath = PathHelper.NormalizePath(filePath);

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                return null;

            var session = FunctionInvokingChatClient.CurrentContext?.Options?.GetSession();
            var state = session?.StateBag.GetValue<LsState>(LsState.StateBagKey);

            int lastSlash = filePath.LastIndexOf('/');
            string parentDir = lastSlash > 0 ? filePath[..lastSlash] : "/";

            if (state is null || !state.HasBeenListed(parentDir))
                return "Error: Parent directory has not been listed yet. Use ls on the parent directory first before creating new files.";

            return null;
        }

        public static void UpdateReadState(string filePath, IFileSystemAccess access)
        {
            filePath = PathHelper.NormalizePath(filePath);

            var session = FunctionInvokingChatClient.CurrentContext?.Options?.GetSession();
            var state = session?.StateBag.GetValue<FileReadState>(FileReadState.StateBagKey);

            DateTime lastWriteTime = access.GetLastWriteTimeUtc(filePath) ?? DateTime.UtcNow;
            state?.RecordRead(filePath, lastWriteTime);
        }
    }
}
