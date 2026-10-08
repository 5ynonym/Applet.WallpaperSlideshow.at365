using System.Drawing;
using Applets.WallpaperSlideshow;

internal static class BackgroundTests
{
    private sealed class FakeApi(bool picture = true, bool span = true, bool fail = false) : IWindowsBackgroundApi
    {
        public string? AppliedPath;
        public bool SpanRequested, Disposed;
        public bool IsPicture => picture;
        public bool IsSpan => span;
        public void SetPicture(string path) { AppliedPath = path; if (fail) throw new InvalidOperationException("fixture failure"); }
        public void SetSpan() { SpanRequested = true; }
        public void Dispose() { Disposed = true; }
    }
    public static void Run(string root)
    {
        var directory = Path.Combine(root, "background");
        Directory.CreateDirectory(directory);
        var cache = Path.Combine(directory, "cache.png");
        using (var image = new Bitmap(12, 8)) { using var graphics = Graphics.FromImage(image); graphics.Clear(Color.Red); image.Save(cache); }
        var api = new FakeApi();
        WindowsBackground.Apply(false, cache, directory, () => api);
        using (var copy = new Bitmap(api.AppliedPath!))
            if (copy.Size != new Size(12, 8) || copy.GetPixel(0, 0).R != 255) throw new Exception("Displayed image not preserved");
        if (!api.SpanRequested || !api.Disposed || !File.Exists(api.AppliedPath)) throw new Exception("Background not applied or lifetime lost");
        api = new FakeApi();
        WindowsBackground.Apply(true, "missing", directory, () => api);
        using (var copy = new Bitmap(api.AppliedPath!))
            if (copy.Size != new Size(1, 1) || copy.GetPixel(0, 0).ToArgb() != Color.Black.ToArgb()) throw new Exception("Paused background not kept black");
        foreach (var failure in new[] { new FakeApi(picture: false), new FakeApi(span: false), new FakeApi(fail: true) }) {
            try { WindowsBackground.Apply(false, cache, directory, () => failure); throw new Exception("Unverified success accepted"); }
            catch (InvalidOperationException) { if (!failure.Disposed) throw new Exception("COM lifetime leaked on failure"); }
        }
        var called = false;
        try { WindowsBackground.Apply(false, "missing", directory, () => { called = true; return new FakeApi(); }); throw new Exception("Missing cache accepted"); }
        catch (InvalidOperationException) { if (called) throw new Exception("Settings mutated without an image"); }
        if (Directory.GetFiles(directory, "background-*.bmp").Length != 0) throw new Exception("Staging image leaked");
        Console.WriteLine("PASS: background cache preservation, paused black image, verified results, API/missing-cache failures, file and COM lifetime");
    }
}
