namespace DeepAgentNet.FileSystems.Internal.Contracts
{
    internal interface IFileLocks
    {
        ValueTask<IDisposable> AcquireAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
