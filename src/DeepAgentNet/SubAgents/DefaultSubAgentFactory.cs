using DeepAgentNet.SubAgents.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.SubAgents
{
    public class DefaultSubAgentFactory(string systemPrompt) : ISubAgentFactory
    {
        public IChatClient? CreateChatClient(ChatOptions defaultOptions) => null;

        public ChatClientAgentOptions ProvideAgentOptions(
            ChatClientAgentOptions defaultOptions, IList<AIContextProvider> defaultContextProviders)
        {
            ChatClientAgentOptions options = defaultOptions.Clone();
            options.ChatOptions ??= new ChatOptions();
            options.ChatOptions.Instructions = systemPrompt;

            options.AIContextProviders = [..options.AIContextProviders ?? [], ..defaultContextProviders];

            return options;
        }

        public AIAgent DecorateAgent(AIAgent agent) => agent;
    }
}
