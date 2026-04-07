using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents.Contracts
{
    public interface ISubAgentFactory
    {
        IChatClient? CreateChatClient(ChatOptions defaultOptions) => null;

        ChatClientAgentOptions ProvideAgentOptions(ChatClientAgentOptions defaultOptions, IList<AIContextProvider> defaultContextProviders)
        {
            var options = defaultOptions.Clone();
            options.AIContextProviders = [..options.AIContextProviders ?? [], ..defaultContextProviders];
            return options;
        }

        AIAgent DecorateAgent(AIAgent agent) => agent;
    }
}
