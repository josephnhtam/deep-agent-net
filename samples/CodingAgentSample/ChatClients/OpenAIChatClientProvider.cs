using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace CodingAgentSample.ChatClients
{
    public class OpenAIChatClientProvider : IChatClientProvider
    {
        public IChatClient GetChatClient()
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException("Set OPENAI_API_KEY environment variable.");
            var modelName = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-5-mini";
            var endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");

            var options = new OpenAIClientOptions();
            if (!string.IsNullOrEmpty(endpoint))
                options.Endpoint = new Uri(endpoint);

            return new OpenAIClient(new ApiKeyCredential(apiKey), options)
                .GetChatClient(modelName)
                .AsIChatClient();
        }
    }
}
