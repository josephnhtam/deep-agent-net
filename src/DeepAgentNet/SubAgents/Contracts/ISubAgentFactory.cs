using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents.Contracts
{
    public interface ISubAgentFactory
    {
        IChatClient? CreateChatClient(ChatOptions defaultOptions);
        IChatClient DecorateChatClient(IChatClient chatClient);
        ChatClientAgentOptions ProvideAgentOptions(ChatClientAgentOptions defaultOptions, IList<AIContextProvider> defaultContextProviders);
        AIAgent DecorateAgent(AIAgent agent);
    }
}
