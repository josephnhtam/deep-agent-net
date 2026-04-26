using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients.DelegatingHandlers;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients
{
    internal static class AzureDynamicSessionsOptionsExtensions
    {
        public static HttpClient CreateHttpClient(this AzureDynamicSessionsOptions options)
        {
            AuthorizationHandler authHandler = new(options.AccessTokenProvider, options.Scopes);

            if (options.ResiliencePipeline != null)
            {
                ResilienceHandler resilienceHandler = new(options.ResiliencePipeline)
                {
                    InnerHandler = new HttpClientHandler()
                };

                authHandler.InnerHandler = resilienceHandler;
            }
            else
            {
                authHandler.InnerHandler = new HttpClientHandler();
            }

            string endpoint = options.PoolManagementEndpoint.TrimEnd('/') + "/";

            return new HttpClient(authHandler)
            {
                BaseAddress = new Uri(endpoint)
            };
        }
    }
}
