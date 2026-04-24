using Microsoft.Extensions.AI;

namespace SampleUtilities.ChatClients
{
    public interface IChatClientProvider
    {
        IChatClient GetChatClient();
    }
}
