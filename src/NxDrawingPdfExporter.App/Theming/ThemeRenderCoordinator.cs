namespace NxDrawingPdfExporter.App.Theming;

internal sealed class ThemeRenderCoordinator(
    Func<ThemeRenderRequest, CancellationToken, ThemeFrame> render,
    Action<ThemeFrame> receive,
    Action<Exception> fail,
    TimeSpan debounce,
    Action<Action>? dispatch = null) : IDisposable
{
    private readonly object sync = new();
    private readonly SemaphoreSlim running = new(1);
    private CancellationTokenSource? current;
    private ThemeFrame? queued;
    private long generation;
    private bool disposed;

    internal async Task Request(ThemeRenderRequest request)
    {
        CancellationTokenSource pending;
        long ticket;
        lock (sync)
        {
            if (disposed)
                return;
            CancelPending();
            ticket = generation;
            current = pending = new();
        }

        ThemeFrame? completed = null;
        try
        {
            await Task.Delay(debounce, pending.Token).ConfigureAwait(false);
            await running.WaitAsync(pending.Token).ConfigureAwait(false);
            try
            {
                completed = await Task.Run(() => render(request, pending.Token), pending.Token).ConfigureAwait(false);
            }
            finally { running.Release(); }

            lock (sync)
            {
                if (disposed || generation != ticket)
                    return;
                queued = completed;
                completed = null;
            }
            Dispatch(() => Deliver(ticket));
        }
        catch (OperationCanceledException) when (pending.IsCancellationRequested) { }
        catch (Exception error)
        {
            lock (sync)
            {
                if (disposed || generation != ticket)
                    return;
                queued?.Dispose();
                queued = null;
            }
            try
            {
                Dispatch(() =>
                {
                    lock (sync)
                        if (!disposed && generation == ticket)
                            fail(error);
                });
            }
            catch (InvalidOperationException dispatchError)
            {
                System.Diagnostics.Debug.WriteLine(dispatchError);
            }
        }
        finally
        {
            completed?.Dispose();
            lock (sync)
            {
                if (ReferenceEquals(current, pending))
                    current = null;
                pending.Dispose();
            }
        }
    }

    private void Dispatch(Action action)
    {
        if (dispatch is null)
            action();
        else
            dispatch(action);
    }

    private void Deliver(long ticket)
    {
        lock (sync)
        {
            if (disposed || generation != ticket || queued is not { } result)
                return;
            queued = null;
            receive(result);
        }
    }

    internal void Cancel()
    {
        lock (sync) CancelPending();
    }

    private void CancelPending()
    {
        generation++;
        current?.Cancel();
        current = null;
        queued?.Dispose();
        queued = null;
    }

    public void Dispose()
    {
        lock (sync)
        {
            disposed = true;
            CancelPending();
        }
    }
}
