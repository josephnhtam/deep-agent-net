using DeepAgentNet.SubAgents.Contracts;

namespace DeepAgentNet.SubAgents
{
    public record SubAgentProviderOptions
    {
        public Func<IList<SubAgent>, string>? ToolDescription { get; init; }
        public GeneralPurposeAgentOptions? GeneralPurposeAgent { get; init; }
        public List<SubAgent>? SubAgents { get; init; }
    }

    public record GeneralPurposeAgentOptions(ISubAgentHandle Handle)
    {
        public string? SystemPrompt { get; init; }
        public string? Description { get; init; }
        public SubAgentFactory? Factory { get; init; }
    }
}
