using DeepAgentNet.SubAgents.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodingAgentSample
{
    public class ExploreSubAgentFactory : ISubAgentFactory
    {
        public IChatClient? CreateChatClient(ChatOptions defaultOptions) => null;

        public ChatClientAgentOptions ProvideAgentOptions(
            ChatClientAgentOptions defaultOptions, IList<AIContextProvider> defaultContextProviders)
        {
            var options = defaultOptions.Clone();
            options.Id = $"explore:{Guid.NewGuid():N}";

            options.ChatOptions ??= new ChatOptions();
            options.ChatOptions.Instructions = """
            You are a file search specialist. You excel at thoroughly navigating and exploring codebases.

            Your strengths:
            - Rapidly finding files using glob patterns
            - Searching code and text with powerful regex patterns
            - Reading and analyzing file contents

            Guidelines:
            - Adapt your search approach based on the thoroughness level specified by the caller
            - Do not create or modify any files
            - Return file paths in your final response

            Complete the user's search request efficiently and report your findings clearly.
            """;

            options.AIContextProviders = [..options.AIContextProviders ?? [], ..defaultContextProviders];

            return options;
        }

        public AIAgent DecorateAgent(AIAgent agent) => agent;
    }
}
