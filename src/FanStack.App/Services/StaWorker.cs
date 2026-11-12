using System.Collections.Concurrent;

namespace FanStack.App.Services;

/// <summary>
/// Thread STA in background per le chiamate COM della shell (estrazione icone, rendering .ico):
/// il lavoro non blocca mai il thread della UI.
/// </summary>
internal sealed class StaWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public StaWorker(string name)
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = name, Priority = ThreadPriority.AboveNormal };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> Run<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                tcs.SetResult(work());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private void Loop()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
            action();
    }

    public void Dispose() => _queue.CompleteAdding();
}
