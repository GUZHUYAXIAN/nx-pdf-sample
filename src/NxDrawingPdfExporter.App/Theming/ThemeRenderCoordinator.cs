namespace NxDrawingPdfExporter.App.Theming;

internal sealed class ThemeRenderCoordinator(
    Func<ThemeRenderRequest, CancellationToken, ThemeFrame> render,
    Action<ThemeFrame> receive,
    Action<Exception> fail,
    TimeSpan debounce) : IDisposable
{
    private readonly object sync = new();
    private readonly SemaphoreSlim running = new(1);
    private CancellationTokenSource? current;
    private bool disposed;

    internal async Task Request(ThemeRenderRequest request)
    {
        CancellationTokenSource pending;
        lock (sync)
        {
            if (disposed)
                return;
            current?.Cancel();
            current = pending = new();
        }

        ThemeFrame? completed = null;
        try
        {
            await Task.Delay(debounce, pending.Token);
            await running.WaitAsync(pending.Token);
            try { completed = await Task.Run(() => render(request, pending.Token), pending.Token); }
            finally { running.Release(); }

            lock (sync)
            {
                if (!disposed && ReferenceEquals(current, pending))
                {
                    receive(completed);
                    completed = null;
                }
            }
        }
        catch (OperationCanceledException) when (pending.IsCancellationRequested) { }
        catch (Exception error)
        {
            lock (sync)
            {
                if (!disposed && ReferenceEquals(current, pending))
                    fail(error);
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

    internal void Cancel()
    {
        lock (sync)
        {
            current?.Cancel();
            current = null;
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            disposed = true;
            Cancel();
        }
    }
}
