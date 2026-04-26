using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients;
using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Clients.Contracts;
using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Internal.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions
{
    public partial class AzureDynamicSessionsProvider : AIContextProvider
    {
        private readonly AzureDynamicSessionsProviderOptions _options;
        private readonly IAzureDynamicSessionsClient _client;
        private readonly string _sessionId;
        private readonly IReadOnlyList<AITool> _tools;

        private IReadOnlyList<string>? _preinstalledPackages;
        private bool _preinstalledPackagesFetched;

        public AzureDynamicSessionsProvider(AzureDynamicSessionsProviderOptions options)
        {
            _options = options;
            _client = new AzureDynamicSessionsClient(options.DynamicSessionOptions);
            _sessionId = options.DynamicSessionOptions.SessionId ?? Guid.NewGuid().ToString();

            var executeCodeToolProvider = new ExecuteCodeToolProvider(
                _client,
                options.FileSystemAccess,
                _sessionId,
                options.DynamicSessionOptions.Language,
                options.ExecuteCodeToolOptions);

            var downloadFileToolProvider = new DownloadFileToolProvider(
                _client,
                options.FileSystemAccess,
                _sessionId,
                options.DownloadFileToolOptions);

            var listFilesToolProvider = new ListFilesToolProvider(
                _client,
                _sessionId,
                options.ListFilesToolOptions);

            _tools =
            [
                executeCodeToolProvider.Tool,
                downloadFileToolProvider.Tool,
                listFilesToolProvider.Tool
            ];
        }

        protected override async ValueTask<AIContext> ProvideAIContextAsync(
            InvokingContext context, CancellationToken cancellationToken = default)
        {
            if (_options.FetchPreinstalledPackages && !_preinstalledPackagesFetched)
            {
                _preinstalledPackagesFetched = true;
                _preinstalledPackages = await FetchPreinstalledPackagesAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            AzureDynamicSessionsInfo info = new(
                _options.DynamicSessionOptions.Language,
                _preinstalledPackages,
                _options.DynamicSessionOptions.AdditionalInstructions);

            return new AIContext
            {
                Instructions = _options.SystemPrompt?.Invoke(info)
                    ?? AzureDynamicSessionsDefaults.GetSystemPrompt(info),
                Tools = _tools,
            };
        }

        private async Task<IReadOnlyList<string>?> FetchPreinstalledPackagesAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                string? code = _options.DynamicSessionOptions.ListPreinstalledPackagesCode;

                if (string.IsNullOrWhiteSpace(code))
                    return null;

                CodeExecutionResult result = await _client
                    .ExecuteCodeAsync(_sessionId, code, cancellationToken)
                    .ConfigureAwait(false);

                string? resultStr = result.ExecutionResult?.ToString();

                if (string.IsNullOrEmpty(resultStr))
                    return null;

                return PackageNameRegex().Matches(resultStr)
                    .Select(m => m.Groups[1].Value)
                    .Order()
                    .ToList();
            }
            catch
            {
                return null;
            }
        }

        [GeneratedRegex(@"'([^']+)',\s*'[^']+'")]
        private static partial Regex PackageNameRegex();
    }
}
