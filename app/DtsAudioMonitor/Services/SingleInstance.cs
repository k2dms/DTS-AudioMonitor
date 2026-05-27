using System.Diagnostics;
using System.Threading;

namespace DtsAudioMonitor.Services;

internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Global\k2dms.DtsAudioMonitor.SingleInstance";
    private const string ActivateEventName = @"Global\k2dms.DtsAudioMonitor.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private readonly CancellationTokenSource _cts = new();
    private readonly Action _onActivate;

    private SingleInstance(Mutex mutex, EventWaitHandle activateEvent, Action onActivate)
    {
        _mutex = mutex;
        _activateEvent = activateEvent;
        _onActivate = onActivate;
        _ = Task.Run(() => ListenForActivate(_cts.Token));
    }

    public static bool TryStart(Action onSecondInstance, Action onActivate, out SingleInstance? instance)
    {
        instance = null;
        var createdNew = false;
        Mutex? mutex = null;

        try
        {
            mutex = new Mutex(true, MutexName, out createdNew);
        }
        catch (AbandonedMutexException)
        {
            mutex = new Mutex(true, MutexName, out _);
            createdNew = true;
        }
        catch
        {
            createdNew = false;
        }

        if (!createdNew)
        {
            mutex?.Dispose();
            SignalActivate();
            onSecondInstance();
            return false;
        }

        StopOtherProcesses();

        var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        instance = new SingleInstance(mutex!, activateEvent, onActivate);
        return true;
    }

    private static void SignalActivate()
    {
        try
        {
            using var evt = EventWaitHandle.OpenExisting(ActivateEventName);
            evt.Set();
        }
        catch
        {
            // First instance not running or event not created yet
        }
    }

    private static void StopOtherProcesses()
    {
        var current = Process.GetCurrentProcess();
        foreach (var p in Process.GetProcessesByName(current.ProcessName))
        {
            if (p.Id == current.Id) continue;
            try { p.Kill(true); } catch { /* ignore */ }
        }
    }

    private void ListenForActivate(CancellationToken ct)
    {
        var handles = new WaitHandle[] { _activateEvent, ct.WaitHandle };
        while (!ct.IsCancellationRequested)
        {
            var idx = WaitHandle.WaitAny(handles, TimeSpan.FromSeconds(2));
            if (idx == 0)
                _onActivate();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
        _mutex.Dispose();
        _activateEvent.Dispose();
        _cts.Dispose();
    }
}
