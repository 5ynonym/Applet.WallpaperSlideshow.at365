namespace at365.WallpaperSlideshow;

internal static class AppLog
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, DateTime> Recent = new();
    public static void Error(string operation, Exception error)
    {
        lock (Gate)
        {
            var key = operation + error.Message;
            var now = DateTime.UtcNow;
            if (Recent.TryGetValue(key, out var last) && now - last < TimeSpan.FromMinutes(1)) return;
            if (Recent.Count >= 128) Recent.Clear();
            Recent[key] = now;
            try
            {
                Directory.CreateDirectory(Const.AppDataFolder);
                var path = Path.Combine(Const.AppDataFolder, "errors.log");
                if (File.Exists(path) && new FileInfo(path).Length >= 1024 * 1024)
                    File.Move(path, path + ".1", true);
                File.AppendAllText(path, $"{now:O} [{operation}] {error}\n");
            }
            catch (Exception) { /* Logging must never interrupt the application. */ }
        }
    }
}
