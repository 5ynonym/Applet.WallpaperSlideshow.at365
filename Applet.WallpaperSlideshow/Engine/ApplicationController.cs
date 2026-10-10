using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace at365.WallpaperSlideshow
{
    public sealed class ApplicationController : IDisposable
    {
        public static ApplicationController Instance => _lazy.Value;
        private static readonly Lazy<ApplicationController> _lazy = new(() => new ApplicationController());

        private System.Windows.Forms.Timer? _maintenanceTimer;
        private FolderWatcher? _folderWatcher;
        private Config? _watcherConfig;
        private Config? _activeWatcherConfig;
        private Task? _watcherTask;
        private System.Windows.Forms.Timer? _uiTimer;
        private Rectangle[]? _lastMonitorBounds;

        private int _requiredInitialize;
        private bool _paused = false;
        private readonly PauseState _pauseState = new();
        private bool _disposed;
        private bool _shutdown;
        private DispatcherForm? _dispatcher;
        public bool IsPaused => _pauseState.IsPaused;
        public event Action? StateChanged;

        private ApplicationController() { }

        public void Initialize(Config config, DispatcherForm dispatcherForm, bool manuallyPaused = false)
        {
            _dispatcher = dispatcherForm;
            _ = dispatcherForm.Handle;
            WallpaperController.Instance.Attach(dispatcherForm);
            _pauseState.Manual = manuallyPaused;
            _pauseState.Remote = SystemInformation.TerminalServerSession;
            _paused = _pauseState.IsPaused;
            ApplyConfig(config);
            SetWallpaperSpanMode();
            InitializeApplication();

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            dispatcherForm.OnRdpConnect = () => SetRemotePause(true);
            dispatcherForm.OnRdpDisconnect = () => SetRemotePause(true);

            if (_uiTimer == null)
            {
                _uiTimer = new System.Windows.Forms.Timer();
                _uiTimer.Tick += (_, _) =>
                {
                    if (_pauseState.IsPaused) return;
                    if (Interlocked.Exchange(ref _requiredInitialize, 0) != 0)
                    {
                        InitializeApplication(true);
                    }
                    else
                    {
                        WallpaperController.Instance.UpdateWallpaper();
                    }
                };
            }

            _uiTimer.Interval = config.IntervalSeconds * 1000;
            if (!_paused) _uiTimer.Start();
            else WallpaperController.ClearWallpaper();
            _maintenanceTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _maintenanceTimer.Tick += (_, _) =>
            {
                if (_disposed) return;
                RefreshWatchers();
            };
            _maintenanceTimer.Start();
        }

        private void ApplyConfig(Config config)
        {
            var screens = StableScreensProvider.Screens;
            config.Validate(screens.Select(s => s.Bounds));
            WallpaperRenderer.Instance.SetConfig(config);
            WallpaperController.Instance.Initialize(config);
            HistoryManager.Instance.SetConfig(config);
            HistoryManager.Instance.EnsureInitialized(screens);
            _watcherConfig = config;
            RefreshWatchers();
            if (_uiTimer != null) _uiTimer.Interval = checked(config.IntervalSeconds * 1000);
            if (_uiTimer != null && _dispatcher != null && !_pauseState.IsPaused)
                WallpaperController.Instance.UpdateWallpaper(true);
        }
        public void Configure(Config config)
        {
            ApplyConfig(config);
        }
        public void Next()
        {
            if (_disposed || _pauseState.IsPaused) return;
            if (Interlocked.Exchange(ref _requiredInitialize, 0) != 0) InitializeApplication(true);
            else WallpaperController.Instance.UpdateWallpaper(true);
        }

        private void RefreshWatchers()
        {
            if (_disposed || (_watcherTask != null && !_watcherTask.IsCompleted)) return;
            var config = _watcherConfig;
            _watcherTask = Task.Run(() =>
            {
                try
                {
                    if (!ReferenceEquals(config, _activeWatcherConfig))
                    {
                        _folderWatcher?.Dispose();
                        _folderWatcher = new FolderWatcher(config?.Monitors.SelectMany(m => m.SourceFolders()) ?? [],
                            () => Interlocked.Exchange(ref _requiredInitialize, 1));
                        _activeWatcherConfig = config;
                    }
                    else _folderWatcher?.Refresh();
                }
                catch (Exception ex) { AppLog.Error("監視の更新", ex); }
            });
        }

        private void InitializeApplication(bool forceInitialize = false)
        {
            bool monitorChanged = HasMonitorConfigChanged();
            if (!monitorChanged && !forceInitialize)
                return;

            StableScreensProvider.Refresh();
            WallpaperController.Instance.Invalidate();
            HistoryManager.Instance.EnsureInitialized(StableScreensProvider.Screens);
            if (!_pauseState.IsPaused)
                WallpaperController.Instance.UpdateWallpaper(true);
        }

        public void TogglePause(bool? forceState = null)
        {
            if (_disposed) return;
            _pauseState.Manual = forceState ?? !_pauseState.Manual;
            ApplyPauseState();
        }

        private void SetRemotePause(bool paused)
        {
            _pauseState.Remote = paused;
            ApplyPauseState();
        }

        private void ApplyPauseState()
        {
            bool target = _pauseState.IsPaused;
            if (target == _paused) return;
            _paused = target;
            if (!target)
            {
                InitializeApplication(true);
                _uiTimer?.Start();
            }
            else
            {
                _uiTimer?.Stop();
                WallpaperController.Instance.Invalidate();
                WallpaperController.ClearWallpaper();
            }

            StateChanged?.Invoke();
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            OnUiThread(() =>
            {
                switch (e.Reason)
                {
                    case SessionSwitchReason.SessionLock:
                        _pauseState.SessionLocked = true;
                        break;
                    case SessionSwitchReason.SessionUnlock:
                        _pauseState.SessionLocked = false;
                        break;
                    case SessionSwitchReason.RemoteConnect:
                    case SessionSwitchReason.RemoteDisconnect:
                        _pauseState.Remote = true;
                        break;
                    case SessionSwitchReason.ConsoleConnect:
                        _pauseState.Remote = false;
                        break;
                }
                ApplyPauseState();
            });
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
            => OnUiThread(() => InitializeApplication());

        private void OnUiThread(Action action)
        {
            var form = _dispatcher;
            if (_disposed || form == null || form.IsDisposed || !form.IsHandleCreated) return;
            if (form.InvokeRequired)
            {
                try
                {
                    form.BeginInvoke(() =>
                    {
                        if (!_disposed && !form.IsDisposed) action();
                    });
                }
                catch (InvalidOperationException) when (_disposed || form.IsDisposed || !form.IsHandleCreated) { }
            }
            else action();
        }

        private bool HasMonitorConfigChanged()
        {
            var screens = Screen.AllScreens
                .OrderBy(s => s.Bounds.Left)
                .ThenBy(s => s.Bounds.Top)
                .ToArray();

            if (_lastMonitorBounds == null ||
                _lastMonitorBounds.Length != screens.Length)
            {
                _lastMonitorBounds = screens.Select(s => s.Bounds).ToArray();
                return true;
            }

            var bounds = screens.Select(s => s.Bounds).ToArray();

            for (int i = 0; i < bounds.Length; i++)
            {
                if (!_lastMonitorBounds[i].Equals(bounds[i]))
                {
                    _lastMonitorBounds = bounds;
                    return true;
                }
            }

            return false;
        }

        private void SetWallpaperSpanMode()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            if (key != null)
            {
                key.SetValue("WallpaperStyle", "22");
                key.SetValue("TileWallpaper", "0");
            }
        }

        public static void ApplicationShutdown()
        {
            Instance.PrepareShutdown();
            Application.Exit();
        }

        internal void PrepareShutdown()
        {
            if (_shutdown) return;
            _shutdown = true;
            try { Dispose(); }
            finally { WallpaperController.ClearWallpaper(); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            if (_dispatcher != null)
            {
                _dispatcher.OnRdpConnect = null;
                _dispatcher.OnRdpDisconnect = null;
            }
            try { _maintenanceTimer?.Dispose(); } catch (Exception ex) { AppLog.Error("設定監視の終了", ex); }
            try { _uiTimer?.Dispose(); } catch (Exception ex) { AppLog.Error("更新タイマーの終了", ex); }
            WallpaperController.Instance.Dispose();
            // A slow network probe must not block shutdown. Dispose after it returns.
            if (_watcherTask != null)
                _ = _watcherTask.ContinueWith(_ => _folderWatcher?.Dispose(), TaskScheduler.Default);
            else _folderWatcher?.Dispose();
        }
    }
}
