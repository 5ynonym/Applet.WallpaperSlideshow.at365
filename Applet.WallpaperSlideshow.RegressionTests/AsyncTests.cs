using System.Collections.Concurrent;
using System.Drawing;
using at365.WallpaperSlideshow;

internal static class AsyncTests
{
    private sealed class Result(int id) : IDisposable
    {
        public int Id { get; } = id;
        public int Disposals;
        public void Dispose() => Interlocked.Increment(ref Disposals);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    public static void Run(string directory)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var callbacks = new ConcurrentQueue<Action>();
        var results = new ConcurrentDictionary<int, Result>();
        var built = new ConcurrentQueue<int>();
        var published = new List<int>();
        int active = 0, maximum = 0;
        using var worker = new LatestWorker<int, Result>((id, token) =>
        {
            maximum = Math.Max(maximum, Interlocked.Increment(ref active));
            try
            {
                built.Enqueue(id);
                if (id is 1 or 5)
                {
                    entered.Set();
                    Check(release.Wait(TimeSpan.FromSeconds(5)), "Worker gate timed out");
                    // Model a decoder / OS call that cannot cancel immediately.
                }
                return results[id] = new Result(id);
            }
            finally { Interlocked.Decrement(ref active); }
        }, callbacks.Enqueue, result => published.Add(result.Id));

        worker.Request(1);
        Check(entered.Wait(TimeSpan.FromSeconds(5)), "Worker did not start");
        worker.Request(99, replace: false); // Timer ticks must not starve active work.
        worker.Request(2);
        worker.Request(3);
        Check(!release.IsSet, "UI calls waited for blocked IO");
        release.Set();
        Check(worker.Completion.Wait(TimeSpan.FromSeconds(5)), "Worker did not finish");
        while (callbacks.TryDequeue(out var callback)) callback();
        Check(built.SequenceEqual(new[] { 1, 3 }) && maximum == 1, "Work overlapped or requests did not coalesce");
        Check(published.SequenceEqual(new[] { 3 }) && results.Values.All(r => r.Disposals == 1), "Stale output published or leaked");

        worker.Request(4);
        Check(worker.Completion.Wait(TimeSpan.FromSeconds(5)), "Queued result timed out");
        worker.Invalidate(); // Pause after completion but before the UI receives it.
        while (callbacks.TryDequeue(out var callback)) callback();
        Check(published.Count == 1 && results[4].Disposals == 1, "Pause failed to discard queued output");

        entered.Reset(); release.Reset();
        worker.Request(5);
        Check(entered.Wait(TimeSpan.FromSeconds(5)), "Shutdown test did not start");
        worker.Dispose(); // Must return while IO is still blocked.
        release.Set();
        Check(worker.Completion.Wait(TimeSpan.FromSeconds(5)), "Disposed worker did not finish");
        while (callbacks.TryDequeue(out var callback)) callback();
        Check(results[5].Disposals == 1 && published.Count == 1, "Shutdown published or leaked output");

        using var failedPost = new LatestWorker<int, Result>((id, _) => results[id] = new Result(id),
            _ => throw new InvalidOperationException("UI closed"), _ => throw new Exception("Unexpected publish"));
        failedPost.Request(6);
        Check(failedPost.Completion.Wait(TimeSpan.FromSeconds(5)) && results[6].Disposals == 1,
            "Failed UI dispatch leaked result");
        RenderScreens(directory);
        UiDispatch();
        ProcessExitCleanup(directory);
        Console.WriteLine("PASS: serialized background work, coalescing, stale output, pause/shutdown, dispatch failure, multi-monitor render");
    }

    private static void RenderScreens(string root)
    {
        var folder = Path.Combine(root, "render-input");
        Directory.CreateDirectory(folder);
        var imagePath = Path.Combine(folder, "red.png");
        using (var image = new Bitmap(60, 20))
        {
            using (var graphics = Graphics.FromImage(image)) graphics.Clear(Color.Red);
            image.Save(imagePath);
        }
        var config = new Config { Monitors = [new MonitorConfig { Folder = folder, Mode = StretchMode.Fill }, new MonitorConfig()] };
        var queues = new QueueManager();
        queues.SetConfig(config);
        queues.ReplaceQueues(QueueManager.Prepare(config, 2));
        var request = new WallpaperController.RenderRequest(config,
            [new Rectangle(-20, 0, 20, 20), new Rectangle(0, 0, 20, 20)], 1);
        string path;
        using (var output = WallpaperController.Render(request, queues, default, root))
        {
            path = output.Path;
            using var bitmap = new Bitmap(path);
            Check(bitmap.Width == 40 && bitmap.GetPixel(10, 10).R > 200 &&
                bitmap.GetPixel(30, 10).ToArgb() == Color.Black.ToArgb(), "Monitor bounds/clip were not preserved");
            Check(output.History.Count == 1, "History does not match rendered images");
        }
        Check(!File.Exists(path), "Rendered file leaked");
        using (var thumbnail = ThumbnailResult.Load(imagePath, new Size(15, 15), default))
        {
            Check(thumbnail.Image?.Size == new Size(15, 5) && thumbnail.Resolution == "60×20", "Thumbnail result incorrect");
            using var cached = (Bitmap)thumbnail.TakeImage()!;
            thumbnail.Dispose();
            Check(cached.GetPixel(5, 2).R > 200, "Thumbnail transfer disposed cache image");
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            using var unexpected = WallpaperController.Render(request, queues, cancellation.Token, root);
            throw new Exception("Cancelled render completed");
        }
        catch (OperationCanceledException) { }
    }

    private static void ProcessExitCleanup(string root)
    {
        var output = Path.Combine(root, "exit-test.bmp");
        var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(AsyncTests).Assembly.Location);
        start.ArgumentList.Add("--temporary-exit");
        start.ArgumentList.Add(output);
        using var process = System.Diagnostics.Process.Start(start)!;
        if (!process.WaitForExit(5000))
        {
            process.Kill();
            process.WaitForExit();
            throw new Exception("ProcessExit cleanup test timed out");
        }
        Check(process.ExitCode == 0 && !File.Exists(output), "ProcessExit left an unfinished BMP");
    }

    private static void UiDispatch()
    {
        using var form = new System.Windows.Forms.Form { ShowInTaskbar = false };
        _ = form.Handle; // Hidden handle only; never show a test window.
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int uiThread = Environment.CurrentManagedThreadId;
        int published = 0;
        using var worker = new LatestWorker<int, Result>((id, _) =>
        {
            if (id == 1)
            {
                entered.Set();
                Check(release.Wait(TimeSpan.FromSeconds(5)), "UI test IO gate timed out");
            }
            return new Result(id);
        }, action => form.BeginInvoke(action), result =>
        {
            Check(Environment.CurrentManagedThreadId == uiThread, "Result applied outside UI thread");
            published = result.Id;
        });
        worker.Request(1);
        Check(entered.Wait(TimeSpan.FromSeconds(5)), "UI test worker not started");
        bool heartbeat = false;
        form.BeginInvoke(() => heartbeat = true);
        System.Windows.Forms.Application.DoEvents();
        Check(heartbeat && !release.IsSet, "UI did not respond during blocked IO");
        worker.Invalidate();
        release.Set();
        Check(worker.Completion.Wait(TimeSpan.FromSeconds(5)), "UI worker not finished");
        System.Windows.Forms.Application.DoEvents();
        Check(published == 0, "Paused result reached UI");
        worker.Request(2);
        Check(worker.Completion.Wait(TimeSpan.FromSeconds(5)), "Resumed UI worker not finished");
        System.Windows.Forms.Application.DoEvents();
        Check(published == 2, "Latest result not applied on UI thread");
    }
}
