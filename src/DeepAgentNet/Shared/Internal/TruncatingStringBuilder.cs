using DeepAgentNet.Shared.Internal.Contracts;
using System.Text;

namespace DeepAgentNet.Shared.Internal
{
    internal class TruncatingStringBuilder : IStringBuilder
    {
        private readonly StringBuilder _sb;
        private readonly int _maxLength;
        private readonly string? _truncationMessage;
        private readonly int _newLineLength;

        public bool IsTruncated { get; private set; }

        public TruncatingStringBuilder(int maxLength, string? truncationMessage = null)
        {
            _sb = new StringBuilder();
            _maxLength = maxLength;
            _truncationMessage = truncationMessage;
            _newLineLength = Environment.NewLine.Length;
        }

        public bool AppendLine(string value)
        {
            if (IsTruncated)
            {
                return false;
            }

            if (_sb.Length + value.Length + _newLineLength > _maxLength)
            {
                _sb.AppendLine(_truncationMessage);
                IsTruncated = true;
                return false;
            }

            _sb.AppendLine(value);
            return true;
        }

        public bool Append(string value)
        {
            if (IsTruncated)
            {
                return false;
            }

            if (_sb.Length + value.Length > _maxLength)
            {
                if (!string.IsNullOrEmpty(_truncationMessage))
                    _sb.AppendLine(_truncationMessage);

                IsTruncated = true;
                return false;
            }

            _sb.Append(value);
            return true;
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }
}
