using DeepAgentNet.Agents.Internal.Contracts;
using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.FileSystems.Internal.Tools;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace DeepAgentNet.FileSystems.Internal
{
    internal class FileSystemPreValidator
    {
        private readonly IFileSystemAccess _access;

        public FileSystemPreValidator(IFileSystemAccess access)
        {
            _access = access;
        }

        public void Register(IFunctionCallPreValidationRegistry registry)
        {
            registry.Register(FileSystemDefaults.WriteFileToolName, (call, ct) =>
                FileWriteToolProvider.ValidateAsync(ExtractFilePath(call), _access, ct));

            registry.Register(FileSystemDefaults.EditFileToolName, (call, ct) =>
                FileEditToolProvider.ValidateAsync(ExtractFilePath(call), _access, ct));

            registry.Register(FileSystemDefaults.OverwriteFileToolName, (call, ct) =>
                FileOverwriteToolProvider.ValidateAsync(ExtractFilePath(call), _access, ct));

            registry.Register(FileSystemDefaults.DeleteFileToolName, (call, ct) =>
                FileDeleteToolProvider.ValidateAsync(ExtractFilePath(call), _access, ct));
        }

        private static string ExtractFilePath(FunctionCallContent call)
        {
            if (call.Arguments?.TryGetValue("filePath", out var filePathVal) == true)
            {
                if (filePathVal is string filePath)
                    return filePath;

                if (filePathVal is JsonElement { ValueKind: JsonValueKind.String } jsonValue &&
                    jsonValue.GetString() is { } filePathFromJsonValue)
                    return filePathFromJsonValue;
            }

            throw new ArgumentException("filePath argument is required and must be a string.");
        }
    }
}
