using System.Threading.Channels;
using AppDock.SDK;

namespace at365.WallpaperSlideshow;

internal static class AppLog
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, DateTime> Recent = new();
    private static HostSession? session;

    public static HostSession Connect(ILogService log)
    {
        lock (Gate)
        {
            if (session is not null) throw new InvalidOperationException("Logging is already connected.");
            Recent.Clear();
            return session = new HostSession(log);
        }
    }

    public static void Error(string operation, Exception error) => _ = Enqueue(operation, error);

    public static async Task ErrorAsync(string operation, Exception error)
    {
        try { await Enqueue(operation, error).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
        catch (TimeoutException) { /* Host delivery must not hold up startup failure. */ }
    }

    private static Task Enqueue(string operation, Exception error)
    {
        lock (Gate)
        {
            var key = operation + error.Message;
            var now = DateTime.UtcNow;
            if (Recent.TryGetValue(key, out var last) && now - last < TimeSpan.FromMinutes(1)) return Task.CompletedTask;
            if (Recent.Count >= 128) Recent.Clear();
            Recent[key] = now;
            var message = $"[{operation}] {error}";
            var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (session is null || !session.Messages.Writer.TryWrite((message, delivered))) {
                WriteDiagnostic(message);
                delivered.SetResult();
            }
            return delivered.Task;
        }
    }

    private static void WriteDiagnostic(string message)
    {
        try { Console.Error.WriteLine(message); }
        catch (Exception) { /* Logging must never interrupt the application. */ }
    }

    internal sealed class HostSession : IAsyncDisposable
    {
        internal readonly Channel<(string Message, TaskCompletionSource Delivered)> Messages = Channel.CreateBounded<(string, TaskCompletionSource)>(new BoundedChannelOptions(128) {
            SingleReader = true, FullMode = BoundedChannelFullMode.Wait,
        });
        private readonly CancellationTokenSource shutdown = new();
        private readonly Task delivery;

        internal HostSession(ILogService log) => delivery = Task.Run(async () => {
            await foreach (var message in Messages.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(1));
                    await log.ErrorAsync(message.Message, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (Exception) { WriteDiagnostic(message.Message); }
                finally { message.Delivered.TrySetResult(); }
            }
        });

        public async ValueTask DisposeAsync()
        {
            lock (Gate)
            {
                if (!ReferenceEquals(session, this)) return;
                session = null;
                Messages.Writer.TryComplete();
            }
            // The host allows two seconds for deactivation. Drain while the RPC connection is open.
            shutdown.CancelAfter(TimeSpan.FromSeconds(1));
            await delivery.ConfigureAwait(false);
            shutdown.Dispose();
        }
    }
}
