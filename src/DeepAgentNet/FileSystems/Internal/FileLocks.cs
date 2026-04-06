using System.Collections.Concurrent;

namespace DeepAgentNet.FileSystems.Internal
{
    internal class FileLocks
    {
        private readonly ConcurrentDictionary<string, RefCountedLock> _locks = new(StringComparer.OrdinalIgnoreCase);

        public async Task<IDisposable> AcquireAsync(string filePath, CancellationToken cancellationToken = default)
        {
            string key = filePath.Replace('\\', '/').TrimEnd('/');

            RefCountedLock entry;
            lock (_locks)
            {
                entry = _locks.GetOrAdd(key, _ => new RefCountedLock());
                entry.RefCount++;
            }

            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Releaser(this, key, entry);
        }

        private void Release(string key, RefCountedLock entry)
        {
            entry.Semaphore.Release();

            lock (_locks)
            {
                entry.RefCount--;
                if (entry.RefCount == 0)
                {
                    _locks.TryRemove(key, out _);
                    entry.Semaphore.Dispose();
                }
            }
        }

        private sealed class RefCountedLock
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);
            public int RefCount { get; set; }
        }

        private sealed class Releaser(FileLocks owner, string key, RefCountedLock entry) : IDisposable
        {
            public void Dispose() => owner.Release(key, entry);
        }
    }
}
