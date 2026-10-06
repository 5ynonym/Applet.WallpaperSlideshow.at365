using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace at365.WallpaperSlideshow;

public sealed class WallpaperController : IDisposable
{
    public static WallpaperController Instance { get; } = new();
    private Config _config = new();
    private long _revision;
    private long _queueRevision = -1;
    private readonly QueueManager _queues = new(); // Only accessed by the worker.
    private LatestWorker<RenderRequest, RenderedWallpaper>? _worker;
    private WallpaperController() { }

    internal sealed record RenderRequest(Config Config, Rectangle[] Bounds, long Revision);

    internal sealed class RenderedWallpaper(string path, List<(int Monitor, string Path)> history,
        int historyLimit) : IDisposable
    {
        private static readonly object FileGate = new();
        private static readonly HashSet<string> PendingFiles = new();
        private static bool _exiting;
        static RenderedWallpaper()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                lock (FileGate)
                {
                    _exiting = true;
                    foreach (var file in PendingFiles)
                        try { File.Delete(file); } catch { }
                    PendingFiles.Clear();
                }
            };
        }
        public string Path { get; } = path;
        public List<(int Monitor, string Path)> History { get; } = history;
        public int HistoryLimit { get; } = historyLimit;
        public FileStream CreateOutput()
        {
            lock (FileGate)
            {
                if (_exiting) throw new OperationCanceledException("アプリケーション終了中です。");
                // Allow ProcessExit to remove even a BMP that is still being written.
                var stream = new FileStream(Path, FileMode.CreateNew, FileAccess.Write,
                    FileShare.Read | FileShare.Delete);
                PendingFiles.Add(Path);
                return stream;
            }
        }
        public void Dispose()
        {
            try
            {
                lock (FileGate) { File.Delete(Path); PendingFiles.Remove(Path); }
            }
            catch (Exception ex) { AppLog.Error("一時壁紙の削除", ex); }
        }
    }

    internal void Attach(DispatcherForm form)
    {
        _worker = new LatestWorker<RenderRequest, RenderedWallpaper>(Build,
            action => form.BeginInvoke(action), Publish);
    }

    public void Initialize(Config config)
    {
        // Caller never mutates a configuration after applying it.
        _config = config;
        Invalidate();
    }

    public void Invalidate()
    {
        _revision++;
        _worker?.Invalidate();
    }

    public void UpdateWallpaper(bool replace = false)
    {
        var bounds = StableScreensProvider.Screens.Select(s => s.Bounds).ToArray();
        _worker?.Request(new RenderRequest(_config, bounds, _revision), replace);
    }

    private RenderedWallpaper Build(RenderRequest request, CancellationToken token)
    {
        _queues.Cancellation = token;
        if (_queueRevision != request.Revision)
        {
            _queues.SetConfig(request.Config);
            _queues.ReplaceQueues(QueueManager.Prepare(request.Config, request.Bounds.Length, token));
            _queueRevision = request.Revision;
        }
        return Render(request, _queues, token, Const.AppDataFolder);
    }

    internal static RenderedWallpaper Render(RenderRequest request, QueueManager queues,
        CancellationToken token, string directory)
    {
        token.ThrowIfCancellationRequested();
        if (request.Bounds.Length == 0) throw new InvalidOperationException("モニターがありません。");
        var virtualBounds = request.Bounds.Aggregate(Rectangle.Union);
        using var bitmap = new Bitmap(virtualBounds.Width, virtualBounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        var renderer = new WallpaperRenderer { Cancellation = token };
        renderer.SetConfig(request.Config);
        var history = new List<(int, string)>();
        for (int i = 0; i < request.Bounds.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var image = queues.GetNextImage(i);
            renderer.ComposeMonitor(i, image, graphics, virtualBounds, request.Bounds,
                queues.GetQueue(i), (monitor, path) => history.Add((monitor, path)));
        }
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        var output = new RenderedWallpaper(System.IO.Path.Combine(directory,
            $"render-{Guid.NewGuid():N}.bmp"), history, request.Config.History.Limit);
        try
        {
            using (var stream = output.CreateOutput()) bitmap.Save(stream, ImageFormat.Bmp);
            token.ThrowIfCancellationRequested();
            return output;
        }
        catch { output.Dispose(); throw; }
    }

    private static void Publish(RenderedWallpaper result)
    {
        try
        {
            File.Move(result.Path, Const.WallpaperPicturePath, true);
            SetWallpaper(Const.WallpaperPicturePath);
            foreach (var item in result.History)
                HistoryManager.Instance.Push(item.Monitor, item.Path, result.HistoryLimit);
        }
        finally { WallpaperRenderer.Instance.OverwriteWithBlack(Const.WallpaperPicturePath); }
    }

    public static void ClearWallpaper()
    {
        try
        {
            WallpaperRenderer.Instance.OverwriteWithBlack(Const.WallpaperPicturePath);
            SetWallpaper(string.Empty);
        }
        catch (Exception ex) { AppLog.Error("壁紙の消去", ex); }
    }

    public void Dispose() => _worker?.Dispose();

    private static void SetWallpaper(string path)
    {
        if (!SystemParametersInfo(0x0014, 0, path, 0x01 | 0x02))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "壁紙設定APIが失敗しました。");
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, string path, uint flags);
}
