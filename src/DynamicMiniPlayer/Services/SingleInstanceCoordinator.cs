using System.Threading;
using System.Windows.Threading;

namespace DynamicMiniPlayer.Services;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\DynamicMiniPlayer.Singleton";
    private const string ToggleEventName = @"Local\DynamicMiniPlayer.Toggle";
    private readonly Mutex? _mutex;
    private readonly EventWaitHandle? _toggleEvent;
    private readonly CancellationTokenSource _cancellation = new();

    private SingleInstanceCoordinator(Mutex? mutex, EventWaitHandle? toggleEvent, bool isPrimary)
    {
        _mutex = mutex;
        _toggleEvent = toggleEvent;
        IsPrimary = isPrimary;
    }

    public bool IsPrimary { get; }

    public static SingleInstanceCoordinator Acquire()
    {
        var mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            var toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleEventName);
            return new SingleInstanceCoordinator(mutex, toggleEvent, true);
        }

        mutex.Dispose();
        try
        {
            using var toggleEvent = EventWaitHandle.OpenExisting(ToggleEventName);
            toggleEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
        return new SingleInstanceCoordinator(null, null, false);
    }

    public void Listen(Dispatcher dispatcher, Action onToggle)
    {
        if (!IsPrimary || _toggleEvent is null)
            return;

        _ = Task.Run(() =>
        {
            var handles = new WaitHandle[] { _toggleEvent, _cancellation.Token.WaitHandle };
            while (WaitHandle.WaitAny(handles) == 0)
                dispatcher.BeginInvoke(onToggle);
        });
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _toggleEvent?.Set();
        _toggleEvent?.Dispose();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex.Dispose();
        }
        _cancellation.Dispose();
    }
}
