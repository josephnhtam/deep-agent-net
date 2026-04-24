using Microsoft.Agents.AI;
using System.Threading.Channels;

namespace SampleUtilities.AgentConsoles
{
    public class AgentConsoleBuilder
    {
        private readonly Channel<AgentEvent> _channel;

        public ConsoleSubAgentHandle SubAgentHandle { get; }
        public ChannelWriter<AgentEvent> ChannelWriter => _channel.Writer;

        private AgentConsoleBuilder()
        {
            _channel = Channel.CreateUnbounded<AgentEvent>();
            SubAgentHandle = new ConsoleSubAgentHandle(_channel.Writer);
        }

        public static AgentConsoleBuilder Create() => new();

        public AgentConsole Build(string title, AIAgent agent, AgentSession session)
        {
            var turnRunner = new AgentTurnRunner(agent, session, _channel.Writer);
            return new AgentConsole(title, turnRunner, _channel.Reader);
        }
    }
}
