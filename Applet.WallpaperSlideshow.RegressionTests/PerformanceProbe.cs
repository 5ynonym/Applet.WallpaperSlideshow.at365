using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using at365.WallpaperSlideshow;

internal static class PerformanceProbe
{
    // Render only: no Windows wallpaper API, registry changes or user image folders.
    public static void Run(string? originalAssembly)
    {
        var root = Path.Combine(Path.GetTempPath(), "WallpaperPerf-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try {
            for (int i = 0; i < 16; i++) {
                using var bitmap = new Bitmap(2560, 1440);
                using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.FromArgb(255, i * 12, 100, 200));
                bitmap.Save(Path.Combine(root, $"image{i}.png"));
            }
            var assembly = originalAssembly is null ? typeof(Config).Assembly : Assembly.LoadFrom(Path.GetFullPath(originalAssembly));
            Type Type(string name) => assembly.GetType("at365.WallpaperSlideshow." + name, true)!;
            var config = Type("Config").GetMethod("Parse", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
                [JsonSerializer.Serialize(new { Monitors = new[] { new { Folder = root, Mode = "Tile", TileCount = 16 } } })]);
            var queues = Activator.CreateInstance(Type("QueueManager"), true)!;
            Type("QueueManager").GetMethod("SetConfig")!.Invoke(queues, [config]);
            var prepared = Type("QueueManager").GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [config, 1, CancellationToken.None]);
            Type("QueueManager").GetMethod("ReplaceQueues", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(queues, [prepared]);
            var request = Activator.CreateInstance(Type("WallpaperController").GetNestedType("RenderRequest", BindingFlags.NonPublic)!,
                [config, new[] { new Rectangle(0, 0, 1920, 1080) }, 1L]);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            using var process = Process.GetCurrentProcess(); process.Refresh();
            long baseline = process.PrivateMemorySize64, peak = baseline;
            var beforeCpu = process.TotalProcessorTime;
            using var done = new CancellationTokenSource();
            var monitor = Task.Run(() => {
                while (!done.IsCancellationRequested) { process.Refresh(); peak = Math.Max(peak, process.PrivateMemorySize64); Thread.Sleep(5); }
            });
            var watch = Stopwatch.StartNew();
            using (var result = (IDisposable)Type("WallpaperController").GetMethod("Render", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
                [request, queues, CancellationToken.None, root])!) { }
            watch.Stop(); done.Cancel(); monitor.GetAwaiter().GetResult(); process.Refresh();
            Console.WriteLine(JsonSerializer.Serialize(new { engine = originalAssembly is null ? "applet" : "original", images = 16,
                sourceSize = "2560x1440", screenSize = "1920x1080", baselinePrivateMiB = Math.Round(baseline / 1048576d, 1),
                peakPrivateMiB = Math.Round(peak / 1048576d, 1), additionalPeakMiB = Math.Round((peak - baseline) / 1048576d, 1),
                renderMilliseconds = watch.ElapsedMilliseconds, cpuMilliseconds = (long)(process.TotalProcessorTime - beforeCpu).TotalMilliseconds }));
        } finally { Directory.Delete(root, true); }
    }
}
