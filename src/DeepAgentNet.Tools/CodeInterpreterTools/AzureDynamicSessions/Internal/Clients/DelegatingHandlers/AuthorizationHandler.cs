using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Contracts;
using System.Net;
using System.Net.Http.Headers;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients.DelegatingHandlers
{
    internal class AuthorizationHandler : DelegatingHandler
    {
        private readonly IAccessTokenProvider _accessTokenProvider;
        private readonly IReadOnlyList<string> _scopes;
        private string? _cachedToken;

        public AuthorizationHandler(IAccessTokenProvider accessTokenProvider, IReadOnlyList<string> scopes)
        {
            _accessTokenProvider = accessTokenProvider;
            _scopes = scopes;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _cachedToken ??= await _accessTokenProvider
                .GetAccessTokenAsync(_scopes, forceRefresh: false, cancellationToken)
                .ConfigureAwait(false);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _cachedToken = await _accessTokenProvider
                    .GetAccessTokenAsync(_scopes, forceRefresh: true, cancellationToken)
                    .ConfigureAwait(false);

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);

                response = await base.SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);
            }

            return response;
        }
    }
}
