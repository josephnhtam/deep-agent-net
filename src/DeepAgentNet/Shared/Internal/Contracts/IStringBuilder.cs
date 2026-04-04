namespace DeepAgentNet.Shared.Internal.Contracts
{
    internal interface IStringBuilder
    {
        bool AppendLine(string value);
        bool Append(string value);
        string ToString();
    }
}
