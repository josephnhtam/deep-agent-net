using Polly;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients.DelegatingHandlers
{
    internal class ResilienceHandler : DelegatingHandler
    {
        private readonly ResiliencePipeline _pipeline;

        public ResilienceHandler(ResiliencePipeline pipeline)
        {
            _pipeline = pipeline;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await _pipeline.ExecuteAsync(
                async (req, ct) => await base.SendAsync(req, ct).ConfigureAwait(false),
                request, cancellationToken
            ).ConfigureAwait(false);
        }
    }
}
