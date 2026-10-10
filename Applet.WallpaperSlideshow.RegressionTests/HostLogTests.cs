using System.Collections.Concurrent;
using System.Diagnostics;
using AppDock.SDK;
using at365.WallpaperSlideshow;

internal static class HostLogTests
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    public static void Run(string root) => VerifyAsync(root).GetAwaiter().GetResult();

    private static async Task VerifyAsync(string root)
    {
        var oldLog = Path.Combine(root, "errors.log");
        File.WriteAllText(oldLog, "historical log");
        var previous = Console.Error;
        using var diagnostics = new StringWriter();
        Console.SetError(TextWriter.Synchronized(diagnostics));
        try
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var log = new TestLog(async (_, token) => { entered.TrySetResult(); await release.Task.WaitAsync(token); });
            var session = AppLog.Connect(log);
            AppLog.Error("image read", new IOException("expected failure"));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            AppLog.Error("image read", new IOException("expected failure"));
            AppLog.Error("watcher", new IOException("expected failure"));
            Check(log.Messages.Count == 1, "Log producers waited for host delivery or sent a duplicate");
            release.SetResult();
            await session.DisposeAsync();
            Check(log.Messages.Count == 2 && log.Messages.First().Contains("[image read] System.IO.IOException: expected failure"),
                "Host logging lost operation/exception or queued errors at shutdown");
            Check(log.Messages.Last().Contains("[watcher]"), "Distinct operations were suppressed");
            AppLog.Error("after stop", new IOException("diagnostic only"));
            Check(log.Messages.Count == 2 && diagnostics.ToString().Contains("after stop"), "Stopped logger still used the host");
            var restarted = new TestLog();
            await using (AppLog.Connect(restarted)) AppLog.Error("image read", new IOException("expected failure"));
            Check(restarted.Messages.Count == 1, "Restart retained the old duplicate filter");

            var failing = new TestLog((_, _) => throw new IOException("host disconnected"));
            await using (AppLog.Connect(failing)) AppLog.Error("failed send", new IOException("original error"));
            Check(diagnostics.ToString().Contains("[failed send] System.IO.IOException: original error"), "Delivery failure lost original diagnostic");
            var recovered = new TestLog();
            await using (AppLog.Connect(recovered)) {
                await AppLog.ErrorAsync("reconnect", new IOException("host available"));
                Check(recovered.Messages.Count == 1, "Async logging returned before the host received the error");
            }
            Check(recovered.Messages.Count == 1, "Failed delivery poisoned later logging");

            entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var blocked = new TestLog((_, _) => { entered.TrySetResult(); return new TaskCompletionSource().Task; });
            session = AppLog.Connect(blocked);
            AppLog.Error("blocked send", new IOException("host stalled"));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            for (int i = 0; i < 140; i++) AppLog.Error("queued " + i, new IOException("bounded queue"));
            Check(diagnostics.ToString().Contains("queued 139"), "Full queue did not fall back to diagnostics");
            var elapsed = Stopwatch.StartNew();
            await session.DisposeAsync();
            Check(elapsed.Elapsed < TimeSpan.FromMilliseconds(1800), "Stalled logging delayed deactivation beyond the host allowance");
            Check(blocked.Messages.Count <= 129, "Log queue grew beyond its limit");
            Check(File.ReadAllText(oldLog) == "historical log" && !File.Exists(oldLog + ".1"), "Host logging modified the old local log");
            File.Delete(oldLog);
            await using (AppLog.Connect(new TestLog())) AppLog.Error("no local file", new IOException("host only"));
            Check(!File.Exists(oldLog), "Host logging created a local errors.log");
            Console.WriteLine("PASS: host error delivery / operation and exception / duplicate suppression / async queue / shutdown drain / restart / failed and stalled host / no local file");
        }
        finally { Console.SetError(previous); }
    }

    private sealed class TestLog(Func<string, CancellationToken, Task>? send = null) : ILogService
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public Task InfoAsync(string message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ErrorAsync(string message, CancellationToken cancellationToken = default)
        {
            Messages.Enqueue(message);
            return send?.Invoke(message, cancellationToken) ?? Task.CompletedTask;
        }
    }
}
