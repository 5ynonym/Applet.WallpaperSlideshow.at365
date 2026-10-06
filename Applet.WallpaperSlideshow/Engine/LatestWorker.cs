namespace at365.WallpaperSlideshow;

// One worker, one pending request, one UI result. Invalidation also disposes
// results already posted to the UI but not yet applied.
internal sealed class LatestWorker<TRequest, TResult>(
    Func<TRequest, CancellationToken, TResult> build,
    Action<Action> post, Action<TResult> publish) : IDisposable where TResult : class, IDisposable
{
    private readonly object _gate = new();
    private TRequest? _pending;
    private bool _hasPending, _running, _disposed;
    private long _version;
    private CancellationTokenSource? _cancellation;
    private TResult? _ready;
    internal Task Completion { get; private set; } = Task.CompletedTask;

    public void Request(TRequest request, bool replace = true)
    {
        lock (_gate)
        {
            if (_disposed || (!replace && (_running || _ready != null))) return;
            InvalidateCore();
            _pending = request;
            _hasPending = true;
            if (_running) return;
            _running = true;
            Completion = Task.Run(Run);
        }
    }

    public void Invalidate()
    {
        lock (_gate) { InvalidateCore(); _hasPending = false; }
    }

    private void InvalidateCore()
    {
        _version++;
        _cancellation?.Cancel();
        _ready?.Dispose();
        _ready = null;
    }

    private void Run()
    {
        while (true)
        {
            TRequest request;
            long version;
            CancellationTokenSource cancellation;
            lock (_gate)
            {
                if (_disposed || !_hasPending) { _running = false; return; }
                request = _pending!;
                _pending = default;
                _hasPending = false;
                version = _version;
                cancellation = _cancellation = new CancellationTokenSource();
            }
            TResult? result = null;
            try
            {
                result = build(request, cancellation.Token);
                lock (_gate)
                {
                    if (!_disposed && version == _version)
                    {
                        _ready = result;
                        result = null;
                        post(() => Publish(version));
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    if (version == _version) { _ready?.Dispose(); _ready = null; }
                }
                AppLog.Error("バックグラウンド更新", ex);
            }
            finally
            {
                result?.Dispose();
                lock (_gate)
                {
                    if (_cancellation == cancellation) _cancellation = null;
                    cancellation.Dispose();
                }
            }
        }
    }

    private void Publish(long version)
    {
        lock (_gate)
        {
            if (_disposed || version != _version) return;
            using var result = _ready;
            _ready = null;
            if (result == null) return;
            try { publish(result); }
            catch (Exception ex) { AppLog.Error("壁紙の適用", ex); }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            InvalidateCore();
            _hasPending = false;
        }
    }
}
