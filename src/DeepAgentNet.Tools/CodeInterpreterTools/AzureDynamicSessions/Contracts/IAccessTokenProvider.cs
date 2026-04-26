namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions.Contracts
{
    public interface IAccessTokenProvider
    {
        ValueTask<string> GetAccessTokenAsync(
            IReadOnlyList<string> scopes,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);
    }
}
