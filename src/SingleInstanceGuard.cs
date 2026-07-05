namespace GitHubWallpaper;

/// <summary>
/// Не даёт запустить второй экземпляр приложения.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Global\rkfsociety.GitHubWallpaper.SingleInstance";

    private readonly Mutex _mutex;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex)
    {
        _mutex = mutex;
    }

    /// <summary>
    /// Пытается занять глобальный mutex. Возвращает <c>false</c>, если экземпляр уже работает.
    /// </summary>
    public static bool TryAcquire(out SingleInstanceGuard? guard)
    {
        Mutex? mutex = null;

        try
        {
            mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                guard = null;
                return false;
            }

            guard = new SingleInstanceGuard(mutex);
            return true;
        }
        catch
        {
            mutex?.Dispose();
            guard = null;
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mutex.ReleaseMutex();
        _mutex.Dispose();
        _disposed = true;
    }
}
