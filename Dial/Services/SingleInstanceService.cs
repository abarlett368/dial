using System.Threading;

namespace Dial.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation = new(false, EventResetMode.AutoReset, @"Local\Dial.ShowSettings");
    private RegisteredWaitHandle? _registration;
    private bool _disposed;

    public bool IsOwner { get; }

    public SingleInstanceService()
    {
        _mutex = new Mutex(true, @"Local\Dial.SingleInstance", out bool createdNew);
        IsOwner = createdNew;
    }

    public void Listen(Action activate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsOwner) throw new InvalidOperationException("Only the owning instance can listen.");
        if (_registration is not null) throw new InvalidOperationException("The activation listener is already registered.");
        _registration = ThreadPool.RegisterWaitForSingleObject(_activation, (_, _) => activate(),
            null, Timeout.Infinite, false);
    }

    public void ActivateOwner()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _activation.Set();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _registration?.Unregister(null);
        _activation.Dispose();
        if (IsOwner) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
