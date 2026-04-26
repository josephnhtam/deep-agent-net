namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients
{
    internal partial class AzureDynamicSessionsClient
    {
        private const string ApiVersion = "2024-10-02-preview";

        private string UploadFileUri(string sessionId) =>
            $"files?api-version={ApiVersion}&identifier={sessionId}";

        private string DownloadFileUri(string sessionId, string fileName) =>
            $"files/{fileName}/content?api-version={ApiVersion}&identifier={sessionId}";

        private string FileMetadataUri(string sessionId, string fileName) =>
            $"files/{fileName}?api-version={ApiVersion}&identifier={sessionId}";

        private string ListFilesUri(string sessionId) =>
            $"files?api-version={ApiVersion}&identifier={sessionId}";

        private string ExecutionUri(string sessionId) =>
            $"executions?api-version={ApiVersion}&identifier={sessionId}";
    }
}
