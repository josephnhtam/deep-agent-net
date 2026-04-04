using DeepAgentNet.Shared.Internal.Contracts;
using System.Text;

namespace DeepAgentNet.Shared.Internal
{
    internal class StandardStringBuilder : IStringBuilder
    {
        private readonly StringBuilder _sb = new();

        public bool AppendLine(string value)
        {
            _sb.AppendLine(value);
            return true;
        }

        public bool Append(string value)
        {
            _sb.Append(value);
            return true;
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }
}
