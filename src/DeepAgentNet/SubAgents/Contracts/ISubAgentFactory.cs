using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents.Contracts
{
    public interface ISubAgentFactory
    {
        IChatClient? CreateChatClient(ChatOptions defaultOptions);
        ChatClientAgentOptions ProvideAgentOptions(ChatClientAgentOptions defaultOptions, IList<AIContextProvider> defaultContextProviders);
        AIAgent DecorateAgent(AIAgent agent);
    }
}
