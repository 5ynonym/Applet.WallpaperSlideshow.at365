using System.Drawing;
using System.Reflection;
using System.Text.Json;
using at365.WallpaperSlideshow;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "--performance") { PerformanceProbe.Run(args.Length > 1 ? args[1] : null); return; }
        if ((args.Length == 1 && args[0] == "--protocol-fixture") || Environment.GetEnvironmentVariable("APPDOCK_WALLPAPER_FIXTURE") == "1") {
            AppletTests.RunProtocolFixture(); return;
        }
        if (args.Length == 2 && args[0] == "--temporary-exit")
        {
            var output = new WallpaperController.RenderedWallpaper(args[1], [], 0);
            var stream = output.CreateOutput();
            stream.Write(new byte[16]);
            stream.Flush();
            Environment.Exit(0); // Exercise ProcessExit with an open output stream.
        }
        var originalData = Const.AppDataFolder;
        var testData = Path.Combine(Path.GetTempPath(), "WallpaperTests-" + Guid.NewGuid());
        Directory.CreateDirectory(testData);
        Const.AppDataFolder = testData;
        try
        {
            AppletTests.Run(testData);
            ValidateConfiguration();
            ValidateReadme();
            KeepLastValidConfiguration();
            PreservePauseReasons();
            ClipDrawing();
            LoadDetachedImages();


            AsyncTests.Run(testData);
            RecoveryTests.Run(testData);
            Console.WriteLine("PASS: configuration, last valid settings, pause reasons, drawing clips, detached images");
        }
        finally
        {
            ApplicationController.Instance.Dispose();
            Const.AppDataFolder = originalData;
            var absolute = Path.GetFullPath(testData);
            if (absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(absolute).StartsWith("WallpaperTests-", StringComparison.Ordinal))
                Directory.Delete(absolute, true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void ValidateConfiguration()
    {
        new Config().Validate();
        foreach (var json in new[]
        {
            "{\"IntervalSeconds\":0}", "{\"IntervalSeconds\":2147483647}",
            "{\"Monitors\":null}", "{\"Monitors\":[null]}", "{\"History\":null}",
            "{\"History\":{\"Limit\":-1}}", "{\"History\":{\"ThumbnailWidth\":0}}",
            "{\"Monitors\":[{\"TileCount\":0}]}", "{\"Monitors\":[{\"Mode\":999}]}",
            "{\"Monitors\":[{\"PaddingLeft\":-1}]}"
        })
        {
            try
            {
                JsonSerializer.Deserialize<Config>(json)!.Validate();
                throw new Exception($"Invalid config accepted: {json}");
            }
            catch (InvalidDataException) { }
        }
        var config = new Config { Monitors = [new MonitorConfig { PaddingLeft = 100 }] };
        try
        {
            config.Validate([new Rectangle(0, 0, 100, 100)]);
            throw new Exception("Empty drawing area accepted");
        }
        catch (InvalidDataException) { }
        config.Monitors[0].PaddingLeft = 99;
        config.Validate([new Rectangle(0, 0, 100, 100)]);
    }

    private static void ValidateReadme()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Applet.WallpaperSlideshow.at365.slnx")))
            directory = directory.Parent;
        Check(directory != null, "Repository root not found");
        var text = File.ReadAllText(Path.Combine(directory!.FullName, "README.md"));
        var example = System.Text.RegularExpressions.Regex.Match(text, "```json\\s*(.*?)```",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Check(example.Success, "README config example missing");
        Config.Parse("{\"Monitors\":" + example.Groups[1].Value + "}");
    }

    private static void KeepLastValidConfiguration()
    {
        // Preparing settings does not publish a wallpaper or terminate processes.
        var apply = typeof(ApplicationController).GetMethod("ApplyConfig", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var active = typeof(WallpaperRenderer).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var valid = new Config();
        apply.Invoke(ApplicationController.Instance, [valid]);
        try
        {
            apply.Invoke(ApplicationController.Instance, [new Config { IntervalSeconds = 0 }]);
            throw new Exception("Invalid configuration was applied");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { }
        Check(ReferenceEquals(valid, active.GetValue(WallpaperRenderer.Instance)), "Active config replaced on failure");
    }

    private static void PreservePauseReasons()
    {
        var state = new PauseState { Manual = true, SessionLocked = true };
        state.SessionLocked = false;
        Check(state.IsPaused, "Unlock cancelled manual pause");
        state.Remote = true;
        state.Manual = false;
        Check(state.IsPaused, "Manual resume bypassed remote pause");
        state.SessionLocked = true;
        state.Remote = false;
        Check(state.IsPaused, "Console reconnect bypassed lock");
        state.SessionLocked = false;
        Check(!state.IsPaused, "All pause reasons cleared but still paused");
    }

    private static void ClipDrawing()
    {
        using var source = new Bitmap(100, 20);
        using (var graphics = Graphics.FromImage(source)) graphics.Clear(Color.Red);
        foreach (var mode in new[] { StretchMode.Fill, StretchMode.Center, StretchMode.Fit, StretchMode.Stretch })
        {
            using var output = new Bitmap(60, 40);
            using var graphics = Graphics.FromImage(output);
            graphics.Clear(Color.Black);
            var clip = graphics.ClipBounds;
            WallpaperRenderer.Instance.DrawImageWithMode(graphics, source, new Rectangle(20, 10, 20, 20), mode);
            Check(graphics.ClipBounds == clip, "Graphics clip was not restored");
            Check(output.GetPixel(19, 20).ToArgb() == Color.Black.ToArgb(), $"{mode}: left overflow");
            Check(output.GetPixel(40, 20).ToArgb() == Color.Black.ToArgb(), $"{mode}: right overflow");
            Check(output.GetPixel(30, 20).R > 200, $"{mode}: image missing");
        }
    }

    private static void LoadDetachedImages()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WallpaperRegression-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var format in new[] { System.Drawing.Imaging.ImageFormat.Png, System.Drawing.Imaging.ImageFormat.Jpeg, System.Drawing.Imaging.ImageFormat.Bmp })
            {
                var path = Path.Combine(directory, "source." + format);
                using (var source = new Bitmap(120, 60))
                {
                    using (var g = Graphics.FromImage(source)) g.Clear(Color.Red);
                    source.Save(path, format);
                }
                using var full = ImageLoader.Load(path);
                using var thumb = ImageLoader.Load(path, new Size(30, 30), out var original);
                File.Delete(path);
                Check(full != null && full.GetPixel(60, 30).R > 200, "Decoded pixels depend on source file");
                Check(thumb?.Size == new Size(30, 15) && original == new Size(120, 60), "Thumbnail dimensions invalid");
                using var saved = new MemoryStream();
                full!.Save(saved, System.Drawing.Imaging.ImageFormat.Png);
            }
            var broken = Path.Combine(directory, "broken.png");
            File.WriteAllText(broken, "invalid image");
            Check(ImageLoader.Load(broken) == null, "Corrupt image accepted");
            Check(ImageLoader.Load(Path.Combine(directory, "missing.png")) == null, "Missing image accepted");
        }
        finally
        {
            foreach (var path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }
    }


}
