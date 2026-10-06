namespace at365.WallpaperSlideshow;

public sealed class HistoryManager
{
    public static HistoryManager Instance { get; } = new();
    private LinkedList<string>[] history = [];
    private Config? config;
    private HistoryManager() { }
    public void SetConfig(Config value) {
        config = value;
        foreach (var list in history) while (list.Count > value.History.Limit) list.RemoveLast();
    }
    public void EnsureInitialized(Screen[] screens) {
        if (history.Length == screens.Length) return;
        var next = new LinkedList<string>[screens.Length];
        for (int i = 0; i < next.Length; i++) next[i] = i < history.Length ? history[i] : new();
        history = next;
    }
    public void Push(int monitor, string path, int limit) {
        if (monitor < 0 || monitor >= history.Length || limit == 0) return;
        var list = history[monitor]; list.AddFirst(path);
        while (list.Count > Math.Min(limit, config?.History.Limit ?? limit)) list.RemoveLast();
    }
    internal sealed record Entry(int Monitor, string Path);
    internal Entry[] Snapshot() => history.SelectMany((list, i) => list.Select(path => new Entry(i, path))).ToArray();
}
