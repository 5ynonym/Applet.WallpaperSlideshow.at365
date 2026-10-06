namespace at365.WallpaperSlideshow;

public sealed class FolderWatcher : IDisposable
{
    private readonly Dictionary<string, FileSystemWatcher?> _watchers;
    private readonly HashSet<string> _failed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action _onChanged;
    private readonly object _gate = new();
    private bool _disposed;

    public FolderWatcher(IEnumerable<string?> folders, Action onChanged)
    {
        _onChanged = onChanged;
        _watchers = folders.Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f!).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(f => f, _ => (FileSystemWatcher?)null, StringComparer.OrdinalIgnoreCase);
        Refresh();
    }

    public void Refresh()
    {
        lock (_gate)
        {
            if (_disposed) return;
            foreach (var folder in _watchers.Keys.ToArray())
            {
                var watcher = _watchers[folder];
                if (watcher != null && !_failed.Contains(folder) && Directory.Exists(folder)) continue;
                watcher?.Dispose();
                _watchers[folder] = null;
                _failed.Remove(folder);
                if (watcher != null) _onChanged();
                if (!Directory.Exists(folder))
                {
                    AppLog.Error("フォルダ監視の再接続待ち: " + folder,
                        new DirectoryNotFoundException("監視対象フォルダにアクセスできません。"));
                    continue;
                }
                try
                {
                    watcher = new FileSystemWatcher(folder)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                            NotifyFilters.LastWrite | NotifyFilters.Size
                    };
                    watcher.Changed += OnChanged;
                    watcher.Created += OnChanged;
                    watcher.Deleted += OnChanged;
                    watcher.Renamed += OnChanged;
                    watcher.Error += (_, e) => MarkFailed(folder, e.GetException());
                    _watchers[folder] = watcher;
                    watcher.EnableRaisingEvents = true;
                    _onChanged();
                }
                catch (Exception ex)
                {
                    watcher?.Dispose();
                    _watchers[folder] = null;
                    AppLog.Error("フォルダ監視: " + folder, ex);
                }
            }
        }
    }

    internal void MarkFailed(string folder, Exception error)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _failed.Add(folder);
            AppLog.Error("フォルダ監視の再接続待ち: " + folder, error);
            _onChanged();
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        lock (_gate)
        {
            if (!_disposed) _onChanged();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var watcher in _watchers.Values) watcher?.Dispose();
            _watchers.Clear();
            _failed.Clear();
        }
    }
}
