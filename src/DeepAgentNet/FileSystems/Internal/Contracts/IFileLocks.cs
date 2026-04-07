namespace DeepAgentNet.FileSystems.Internal.Contracts
{
    public interface IFileLocks
    {
        ValueTask<IDisposable> AcquireAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
