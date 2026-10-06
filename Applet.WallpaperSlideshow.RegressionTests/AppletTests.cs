using System.Drawing;
using System.Text;
using System.Text.Json;
using AppDock.SDK;
using AppDock.Runtime;
using Applets.WallpaperSlideshow;
using at365.WallpaperSlideshow;

internal static class AppletTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run(string root)
    {
        var parent = Path.Combine(root, "sources");
        var child = Path.Combine(parent, "child");
        var other = Path.Combine(root, "other");
        Directory.CreateDirectory(child); Directory.CreateDirectory(other);
        foreach (var path in new[] { Path.Combine(parent, "a.png"), Path.Combine(child, "b.png"), Path.Combine(other, "c.png") }) {
            using var image = new Bitmap(30, 20);
            using (var graphics = Graphics.FromImage(image)) graphics.Clear(Color.Red);
            image.Save(path);
        }
        var settings = new TestSettings();
        settings.Values["monitors"] = JsonSerializer.Serialize(new[] { new { Folders = new[] { parent, child, parent.ToUpperInvariant(), other, Path.Combine(root, "missing") }, Folder = parent, Mode = "Tile", TileCount = 3 } });
        var config = SlideshowOptions.Read(settings);
        Check(config.Monitors[0].SourceFolders().Count() == 4, "Duplicate source folders retained");
        var queues = QueueManager.Prepare(config, 1);
        Check(queues[0].Count == 3 && queues[0].Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3, "Combined sources duplicated or lost images");
        var manager = new QueueManager(); manager.SetConfig(config); manager.ReplaceQueues(queues);
        var cycle = Enumerable.Range(0, 3).Select(_ => manager.GetNextImage(0)).ToArray();
        Check(cycle.Distinct().Count() == 3, "Repeated image before completing cycle");
        using (var result = WallpaperController.Render(new(config, [new Rectangle(0, 0, 120, 80)], 1), manager, default, root)) {
            Check(result.History.Count == 3, "Tile renderer did not consume combined folders");
            using var bitmap = new Bitmap(result.Path);
            Check(Enumerable.Range(0, bitmap.Width).Any(x => Enumerable.Range(0, bitmap.Height).Any(y => bitmap.GetPixel(x, y).R > 100)), "Tile renderer produced no image");
        }
        foreach (var json in new[] { "[null]", "[{\"Folders\":null}]", "[{\"Folders\":[\"\"]}]", "[{\"Folders\":[null]}]" }) {
            settings.Values["monitors"] = json;
            try { SlideshowOptions.Read(settings); throw new Exception("Invalid sources accepted: " + json); }
            catch (InvalidDataException) { }
        }
        settings.Values["monitors"] = JsonSerializer.Serialize(new[] { new { Folder = parent } });
        Check(QueueManager.Prepare(SlideshowOptions.Read(settings), 1)[0].Count == 2, "Legacy Folder setting lost");
        settings.Values["monitors"] = new[] { new { Folder = parent, Mode = 4, TileCount = 13, PaddingLeft = 1, PaddingRight = 2, PaddingTop = 3, PaddingBottom = 40 } };
        settings.Values["thumbnailWidth"] = 1000; settings.Values["thumbnailHeight"] = 1000; settings.Values["maxFileNameLength"] = 1024;
        var structured = SlideshowOptions.Read(settings);
        Check(structured.Monitors[0].Mode == StretchMode.Tile && structured.Monitors[0].TileCount == 13 && structured.Monitors[0].PaddingBottom == 40, "Structured/legacy per-monitor settings lost");
        Check(structured.History.ThumbnailWidth == 1000 && structured.History.MaxFileNameLength == 1024, "Original history settings constrained");
        settings.Values["monitors"] = new[] { new { Folder = "relative/path" } };
        Check(SlideshowOptions.Read(settings).Monitors[0].SourceFolders().Single() == Path.GetFullPath("relative/path"), "Legacy relative path lost");
        HistoryManager.Instance.EnsureInitialized(Screen.AllScreens);
        HistoryManager.Instance.SetConfig(new Config { History = new HistoryConfig { Limit = 2 } });
        for (int i = 0; i < 5; i++) HistoryManager.Instance.Push(0, "item" + i, 2);
        Check(HistoryManager.Instance.Snapshot().Select(e => e.Path).SequenceEqual(new[] { "item4", "item3" }), "History not bounded or newest-first");
        HistoryManager.Instance.Remove("ITEM4");
        Check(HistoryManager.Instance.Snapshot().Select(e => e.Path).SequenceEqual(new[] { "item3" }), "Deleted history not removed");
        HistoryManager.Instance.SetConfig(new Config { History = new HistoryConfig { Limit = 0 } });
        HistoryManager.Instance.Push(0, "ignored", 0);
        Check(HistoryManager.Instance.Snapshot().Length == 0, "Disabled history retained entries");
        Console.WriteLine("PASS: multiple sources, nested/duplicate/missing folders, legacy settings, no-repeat cycle, sequential tiles, bounded history");
    }
    private sealed class TestSettings : ISettingsService
    {
        public Dictionary<string, object> Values { get; } = new();
        public T Get<T>(string key, T fallback) => Values.TryGetValue(key, out var value) ? JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))! : fallback;
        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) { Values[key] = value!; return Task.CompletedTask; }
        public IDisposable OnChanged(Func<CancellationToken, Task> handler) => throw new NotSupportedException();
        public Task SetOptionsAsync(string key, IReadOnlyList<SettingOption> options, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class FixtureEngine(string folder) : ISlideshowController
    {
        public bool IsPaused { get; private set; }
        public void Start(Config config, bool paused) {
            IsPaused = paused;
            HistoryManager.Instance.EnsureInitialized(Screen.AllScreens);
            HistoryManager.Instance.SetConfig(config);
            for (int i = 0; i < 7; i++) HistoryManager.Instance.Push(0, Path.Combine(folder, $"image{i}.png"), config.History.Limit);
            HistoryManager.Instance.Push(1, Path.Combine(folder, "image3.png"), config.History.Limit);
        }
        public void Configure(Config config) { HistoryManager.Instance.SetConfig(config); }
        public void Pause(bool paused) { IsPaused = paused; }
        public void Next() { if (!IsPaused) Console.Error.WriteLine("FIXTURE_NEXT_WALLPAPER"); }
        public void Stop() { }
    }
    public static void RunProtocolFixture()
    {
        Console.InputEncoding = Encoding.UTF8; Console.OutputEncoding = new UTF8Encoding(false);
        var protocol = Console.Out; Console.SetOut(Console.Error);
        var folder = Path.Combine(Path.GetTempPath(), "WallpaperFixture-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        for (int i = 0; i < 7; i++) {
            using var image = i == 0 ? new Bitmap(1500, 1000) : new Bitmap(64, 48);
            using (var graphics = Graphics.FromImage(image)) graphics.Clear(Color.FromArgb(255, 30 * i, 120, 220 - 20 * i));
            if (i == 0) {
                var pixels = new byte[image.Width * image.Height * 4]; new Random(1).NextBytes(pixels);
                for (int pixel = 3; pixel < pixels.Length; pixel += 4) pixels[pixel] = 255;
                var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); image.UnlockBits(data);
            }
            image.Save(Path.Combine(folder, $"image{i}.png"));
        }
        using var form = new Form { ShowInTaskbar = false }; _ = form.Handle;
        var applet = new WallpaperApplet(form, new FixtureEngine(folder), File.Delete);
        var session = Task.Run(async () => {
            try { await AppletSession.RunAsync(applet, Console.In, protocol); }
            finally { await form.InvokeAsync(Application.ExitThread); }
        });
        Application.Run(new ApplicationContext());
        session.GetAwaiter().GetResult();
        Directory.Delete(folder, true);
    }
}
