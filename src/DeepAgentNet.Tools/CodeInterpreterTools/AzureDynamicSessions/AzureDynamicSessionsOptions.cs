using DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Contracts;
using Polly;

namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions
{
    public record AzureDynamicSessionsOptions
    {
        public const string DefaultScope = "https://dynamicsessions.io/.default";

        public required string PoolManagementEndpoint { get; init; }
        public required IAccessTokenProvider AccessTokenProvider { get; init; }
        public IReadOnlyList<string> Scopes { get; init; } = [DefaultScope];

        public ResiliencePipeline? ResiliencePipeline { get; init; }
        public string? SessionId { get; init; }
        public string Language { get; init; } = "Python";
        public string? AdditionalInstructions { get; init; } =
            """To install additional packages, use `subprocess.check_call(["pip", "install", "package_name"])` in your code. """ +
            """Prefer preinstalled packages when available.""";

        public string? ListPreinstalledPackagesCode { get; init; } =
            """import pkg_resources; [(d.project_name, d.version) for d in pkg_resources.working_set]""";
    }
}
