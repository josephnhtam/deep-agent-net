using Microsoft.Extensions.AI;

namespace DeepAgentNet.Agents
{
    public delegate IChatClient DecorateChatClientDelegate(IChatClient chatClient);
}
