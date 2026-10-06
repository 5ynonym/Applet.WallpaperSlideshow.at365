using System.Reflection;
using at365.WallpaperSlideshow;

internal static class RecoveryTests
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    public static void Run(string root)
    {

        RecoverWatcher(root);
        ScanPartialTree();
        RotateLog(root);
        DisposeController();
        Console.WriteLine("PASS: watcher recovery/disposal, partial scan, logging, controller disposal");
    }

    private static void RecoverWatcher(string root)
    {
        var folder = Path.Combine(root, "images");
        int changes = 0;
        using var signal = new ManualResetEventSlim();
        using var watcher = new FolderWatcher([folder], () => { Interlocked.Increment(ref changes); signal.Set(); });
        Directory.CreateDirectory(folder);
        watcher.Refresh();
        Check(changes > 0, "Missing folder did not reconnect");
        signal.Reset();
        File.WriteAllText(Path.Combine(folder, "one.png"), "test");
        Check(signal.Wait(TimeSpan.FromSeconds(5)), "Reconnected watcher received no file events");
        watcher.MarkFailed(folder, new InternalBufferOverflowException("simulated overflow"));
        watcher.Refresh();
        signal.Reset();
        File.WriteAllText(Path.Combine(folder, "two.png"), "test");
        Check(signal.Wait(TimeSpan.FromSeconds(5)), "Watcher did not recover from error");
        File.Delete(Path.Combine(folder, "one.png"));
        File.Delete(Path.Combine(folder, "two.png"));
        Directory.Delete(folder);
        watcher.Refresh();
        Directory.CreateDirectory(folder);
        watcher.Refresh();
        signal.Reset();
        File.WriteAllText(Path.Combine(folder, "three.png"), "test");
        Check(signal.Wait(TimeSpan.FromSeconds(5)), "Recreated folder not watched");
        watcher.Dispose();
        var before = changes;
        watcher.MarkFailed(folder, new IOException("after disposal"));
        watcher.Refresh();
        Check(changes == before, "Disposed watcher delivered callbacks");
    }

    private static void ScanPartialTree()
    {
        static IEnumerable<string> Files(string path)
        {
            if (path == "denied") throw new UnauthorizedAccessException("simulated denial");
            return [path + ".png", path + ".txt"];
        }
        var files = ImageCatalog.Scan("root", Files,
            path => path == "root" ? ["denied", "good"] : Array.Empty<string>());
        Check(files.Order().SequenceEqual(new[] { "good.png", "root.png" }), "Inaccessible child discarded readable files");
    }

    private static void RotateLog(string root)
    {
        var path = Path.Combine(root, "errors.log");
        File.WriteAllText(path, new string('x', 1024 * 1024));
        AppLog.Error("rotation-test", new IOException("expected error"));
        Check(File.Exists(path + ".1") && File.ReadAllText(path).Contains("rotation-test"), "Log rotation failed");
        var length = new FileInfo(path).Length;
        AppLog.Error("rotation-test", new IOException("expected error"));
        Check(new FileInfo(path).Length == length, "Repeated error not throttled");
    }

    private static void DisposeController()
    {
        var controller = ApplicationController.Instance;
        var field = typeof(ApplicationController).GetField("_maintenanceTimer", BindingFlags.NonPublic | BindingFlags.Instance)!;
        using var timer = new System.Windows.Forms.Timer();
        int disposed = 0;
        timer.Disposed += (_, _) => disposed++;
        field.SetValue(controller, timer);
        controller.Dispose();
        controller.Dispose();
        Check(disposed == 1, "Shutdown did not dispose maintenance timer exactly once");
    }
}
