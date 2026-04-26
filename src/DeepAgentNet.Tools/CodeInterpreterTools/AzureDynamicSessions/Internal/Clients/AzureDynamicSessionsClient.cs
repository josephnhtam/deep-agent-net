using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients.Contracts;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients
{
    internal partial class AzureDynamicSessionsClient : IAzureDynamicSessionsClient
    {
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public AzureDynamicSessionsClient(AzureDynamicSessionsOptions options)
        {
            _httpClient = options.CreateHttpClient();
        }

        public async Task<CodeExecutionResult> ExecuteCodeAsync(
            string sessionId, string code, CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage message = new(HttpMethod.Post, ExecutionUri(sessionId));

            message.Content = JsonContent.Create(new
            {
                codeInputType = "Inline",
                executionType = "Synchronous",
                code,
                timeoutInSeconds = 220
            }, options: JsonOptions);

            HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken)
                .ConfigureAwait(false);

            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            var execution = await response.Content
                .ReadFromJsonAsync<ExecutionResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return execution!.Result ?? new CodeExecutionResult(execution.Status, null, null, null, null);
        }

        public async Task UploadFileAsync(string sessionId, string fileName,
            Stream content, CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage message = new(HttpMethod.Post, UploadFileUri(sessionId));

            message.Content = new MultipartFormDataContent
            {
                new StreamContent(content)
                {
                    Headers =
                    {
                        { "Content-Disposition", $"form-data; name=\"file\"; filename=\"{fileName}\"" },
                        { "Content-Type", "application/octet-stream" }
                    }
                }
            };

            HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken)
                .ConfigureAwait(false);

            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Stream> DownloadFileAsync(
            string sessionId, string fileName, CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage message = new(HttpMethod.Get, DownloadFileUri(sessionId, fileName));

            HttpResponseMessage response = await _httpClient.SendAsync(
                    message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            return await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<SessionFile> GetFileMetadataAsync(
            string sessionId, string fileName, CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage message = new(HttpMethod.Get, FileMetadataUri(sessionId, fileName));

            HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken)
                .ConfigureAwait(false);

            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            return (await response.Content
                .ReadFromJsonAsync<SessionFile>(JsonOptions, cancellationToken)
                .ConfigureAwait(false))!;
        }

        public async Task<List<SessionFile>> ListFilesAsync(
            string sessionId, CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage message = new(HttpMethod.Get, ListFilesUri(sessionId));

            HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken)
                .ConfigureAwait(false);

            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            var wrapper = await response.Content
                .ReadFromJsonAsync<ValueResponse<SessionFile>>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return [.. wrapper!.Value];
        }

        private static async Task EnsureSuccessAsync(
            HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (!response.IsSuccessStatusCode)
            {
                string? content = null;

                try
                {
                    content = await response.Content.ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch { }

                throw new AzureDynamicSessionsApiException(response.StatusCode, content);
            }
        }

        private record ExecutionResponse(string Status, CodeExecutionResult? Result);
    }
}
