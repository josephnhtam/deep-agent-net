namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients.Contracts
{
    internal interface IAzureDynamicSessionsClient
    {
        Task<CodeExecutionResult> ExecuteCodeAsync(
            string sessionId, string code, CancellationToken cancellationToken = default);

        Task UploadFileAsync(string sessionId, string fileName,
            Stream content, CancellationToken cancellationToken = default);

        Task<Stream> DownloadFileAsync(
            string sessionId, string fileName, CancellationToken cancellationToken = default);

        Task<SessionFile> GetFileMetadataAsync(
            string sessionId, string fileName, CancellationToken cancellationToken = default);

        Task<List<SessionFile>> ListFilesAsync(
            string sessionId, CancellationToken cancellationToken = default);
    }
}
