using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using System.ClientModel;

namespace CodingAgentSample.ChatClients
{
    public class AzureOpenAIChatClientProvider : IChatClientProvider
    {
        public IChatClient GetChatClient()
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException("Set OPENAI_API_KEY environment variable.");
            var endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")
                ?? throw new InvalidOperationException("Set OPENAI_ENDPOINT environment variable.");
            var modelName = Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

            return new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
                .GetChatClient(modelName)
                .AsIChatClient();
        }
    }
}
