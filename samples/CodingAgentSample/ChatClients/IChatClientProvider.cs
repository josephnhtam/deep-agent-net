using Microsoft.Extensions.AI;

namespace CodingAgentSample.ChatClients
{
    public interface IChatClientProvider
    {
        IChatClient GetChatClient();
    }
}
