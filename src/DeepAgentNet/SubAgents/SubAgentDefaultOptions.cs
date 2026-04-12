using DeepAgentNet.Agents.Internal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents
{
    public record SubAgentDefaultOptions(
        IChatClient DefaultChatClient,
        ChatClientAgentOptions DefaultOptions,
        IList<AIContextProvider> DefaultContextProviders,
        DeepAgentOptions DeepAgentOptions
    );
}
