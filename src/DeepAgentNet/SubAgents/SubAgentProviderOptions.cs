using DeepAgentNet.SubAgents.Contracts;

namespace DeepAgentNet.SubAgents
{
    public record SubAgentProviderOptions
    {
        public string? SystemPrompt { get; init; }
        public Func<IList<SubAgent>, string>? TaskToolDescription { get; init; }
        public GeneralPurposeAgentOptions? GeneralPurposeAgent { get; init; }
        public IReadOnlyList<SubAgent>? SubAgents { get; init; }
    }

    public record GeneralPurposeAgentOptions(ISubAgentHandle Handle)
    {
        public string? SystemPrompt { get; init; }
        public string? Description { get; init; }
        public ISubAgentFactory? Factory { get; init; }
        public SubAgentOptions? Options { get; init; }
    }
}
