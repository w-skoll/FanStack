namespace FanStack.App.Services;

/// <summary>
/// Una sola istanza per stack. Se il ventaglio è già aperto (o si sta chiudendo perché si è
/// cliccato di nuovo sull'icona della taskbar), il nuovo processo chiede la chiusura ed esce:
/// così il secondo clic sull'icona chiude il ventaglio invece di riaprirlo.
/// </summary>
internal sealed class StackInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _closeEvent;
    private readonly RegisteredWaitHandle _registration;

    public event Action? CloseRequested;

    private StackInstanceGuard(Mutex mutex, EventWaitHandle closeEvent)
    {
        _mutex = mutex;
        _closeEvent = closeEvent;
        _registration = ThreadPool.RegisterWaitForSingleObject(_closeEvent, (_, _) => CloseRequested?.Invoke(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Restituisce la guardia se questo è l'unico processo per lo stack, altrimenti null.</summary>
    public static StackInstanceGuard? TryAcquire(string appId)
    {
        var baseName = @"Local\" + appId;
        // Solo l'esistenza del mutex conta: nessun possesso, quindi nessun vincolo di thread.
        var mutex = new Mutex(false, baseName + ".instance", out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            if (EventWaitHandle.TryOpenExisting(baseName + ".close", out var existing))
            {
                using (existing)
                    existing.Set();
            }
            return null;
        }
        var closeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, baseName + ".close");
        return new StackInstanceGuard(mutex, closeEvent);
    }

    public void Dispose()
    {
        _registration.Unregister(null);
        _closeEvent.Dispose();
        _mutex.Dispose();
    }
}
