using System.Net;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions
{
    public class AzureDynamicSessionsApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string? ResponseContent { get; }

        public AzureDynamicSessionsApiException(HttpStatusCode statusCode, string? responseContent)
            : base($"Azure Dynamic Sessions API request failed with status {(int)statusCode} ({statusCode}).{(responseContent is not null ? $" {responseContent}" : "")}")
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
        }

        public AzureDynamicSessionsApiException(HttpStatusCode statusCode, string? responseContent, Exception innerException)
            : base($"Azure Dynamic Sessions API request failed with status {(int)statusCode} ({statusCode}).{(responseContent is not null ? $" {responseContent}" : "")}", innerException)
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
        }
    }
}
