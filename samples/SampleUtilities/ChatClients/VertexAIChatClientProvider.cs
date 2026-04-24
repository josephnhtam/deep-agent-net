using Google.GenAI;
using Microsoft.Extensions.AI;

namespace SampleUtilities.ChatClients
{
    public class VertexAIChatClientProvider : IChatClientProvider
    {
        public IChatClient GetChatClient()
        {
            var apiKey = Environment.GetEnvironmentVariable("VERTEX_AI_API_KEY")
                ?? throw new InvalidOperationException("Set VERTEX_AI_API_KEY environment variable.");
            var modelName = Environment.GetEnvironmentVariable("VERTEX_AI_MODEL");

            return new Client(vertexAI: true, apiKey: apiKey).AsIChatClient(modelName);
        }
    }
}
