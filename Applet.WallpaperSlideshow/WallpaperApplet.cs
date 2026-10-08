using System.Diagnostics;
using AppDock.SDK;
using at365.WallpaperSlideshow;
using Microsoft.Win32;
using DockPanel = AppDock.SDK.Panel;

namespace Applets.WallpaperSlideshow;

internal sealed class WallpaperApplet(Form dispatcher, ISlideshowController engine, Action<string>? deleteImage = null) : IAppDockExtension, IPanelActionHandler
{
    private IExtensionContext? context;
    private IDisposable? subscription;
    private Config? config;
    private string? fingerprint;
    private bool historyVisible;
    private int historyPage;
    private int historyTotal;
    private HistoryManager.Entry[] visibleHistory = [];
    private PanelImage[] historyImages = [];
    private bool started;
    private string? pendingDeletion;
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private const int MaximumPageSize = 1000;
    private int loadedPageSize = 4;
    private int historyMonitor;
    private readonly Dictionary<int, int> monitorPages = new();
    private string? imageFolder;
    private string[] imageFiles = [];
    private string historyGeneration = "";
    public async Task ActivateAsync(IExtensionContext services, CancellationToken token)
    {
        context = services;
        void Register(string id, string title, Func<CancellationToken, Task> action) => services.Commands.Register(id, title, async ct => {
            await commandGate.WaitAsync(ct);
            try { await action(ct); } finally { commandGate.Release(); }
        });
        var options = SlideshowOptions.Read(services.Settings);
        imageFolder = Path.Combine(await services.Ui.GetImageDirectoryAsync(token), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(imageFolder);
        await dispatcher.InvokeAsync(() => {
            options.Validate(StableScreensProvider.Screens.Select(s => s.Bounds));
            started = true;
            engine.Start(options, services.Settings.Get("paused", false));
            config = options;
            fingerprint = SlideshowOptions.Fingerprint(options);
        }, token);
        Register(services.ExtensionId + ".start", "壁紙スライドショーを開始／再開", ct => PauseAsync(false, ct));
        Register(services.ExtensionId + ".stop", "壁紙スライドショーを停止", ct => PauseAsync(true, ct));
        Register(services.ExtensionId + ".next", "次の壁紙に更新", async ct => {
            await dispatcher.InvokeAsync(engine.Next, ct); await PublishAsync(ct);
        });
        Register(services.ExtensionId + ".prepare-background", "Windowsの背景を「画像・スパン」に設定", async ct => {
            try { await dispatcher.InvokeAsync(engine.PrepareWindowsBackground, ct); }
            catch (Exception error) when (error is not OperationCanceledException) {
                AppLog.Error("Windowsの背景設定", error);
                throw new InvalidOperationException("背景設定に失敗しました。" + error.Message + " " + WindowsBackground.ManualInstructions, error);
            }
        });
        Register(services.ExtensionId + ".background-settings", "Windowsの背景設定を開く",
            ct => dispatcher.InvokeAsync(engine.OpenWindowsBackgroundSettings, ct));
        Register(services.ExtensionId + ".toggle", "壁紙スライドショーの開始／停止を切り替え", ct => PauseAsync(!services.Settings.Get("paused", false), ct));
        Register(services.ExtensionId + ".history", "最近使った壁紙を表示", ct => ShowHistoryAsync(0, ct));
        Register(services.ExtensionId + ".history.previous", "壁紙履歴の前のページ", ct => ShowHistoryAsync(historyPage - 1, ct));
        Register(services.ExtensionId + ".history.next", "壁紙履歴の次のページ", ct => ShowHistoryAsync(historyPage + 1, ct));
        Register(services.ExtensionId + ".home", "壁紙の操作に戻る", async ct => {
            pendingDeletion = null; historyVisible = false; historyImages = []; visibleHistory = []; await PublishAsync(ct); ClearImages();
        });
        Register(services.ExtensionId + ".data", "壁紙のデータフォルダーを開く", async ct => {
            await dispatcher.InvokeAsync(() => {
                Directory.CreateDirectory(Const.AppDataFolder);
                Process.Start(new ProcessStartInfo(Const.AppDataFolder) { UseShellExecute = true });
            }, ct);
        });
        subscription = services.Settings.OnChanged(async ct => {
            await commandGate.WaitAsync(ct);
            try { await ApplyAsync(ct); } finally { commandGate.Release(); }
        });
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        ApplicationController.Instance.StateChanged += StateChanged;
        await PublishAsync(token);
    }
    private async Task PauseAsync(bool paused, CancellationToken token)
    {
        var services = context ?? throw new InvalidOperationException("Appletは停止しています。");
        await services.Settings.SetAsync("paused", paused, token);
        // Playback commands must still work when a saved image configuration is invalid.
        await dispatcher.InvokeAsync(() => engine.Pause(paused), token);
        await PublishAsync(token);
    }
    private async Task ApplyAsync(CancellationToken token)
    {
        var services = context;
        if (services is null) return;
        try {
            var options = SlideshowOptions.Read(services.Settings);
            var refreshHistory = (services.Settings.Get("historyPageSize", 4) != loadedPageSize);
            await dispatcher.InvokeAsync(() => {
                options.Validate(StableScreensProvider.Screens.Select(s => s.Bounds));
                var nextFingerprint = SlideshowOptions.Fingerprint(options);
                if (nextFingerprint != fingerprint) { engine.Configure(options); fingerprint = nextFingerprint; config = options; refreshHistory = true; }
                engine.Pause(services.Settings.Get("paused", false));
            }, token);
            if (refreshHistory && historyVisible && pendingDeletion is null) await ShowHistoryAsync(historyPage, token);
            else await PublishAsync(token);
        } catch (Exception error) when (error is not OperationCanceledException) {
            AppLog.Error("AppDock設定の反映", error);
            await services.Log.ErrorAsync("設定を反映できません。前回の設定を維持します: " + error.Message, token);
            await PublishAsync(token, error.Message);
        }
    }
    private async Task PublishAsync(CancellationToken token, string? error = null)
    {
        var services = context;
        if (services is null) return;
        var panel = await dispatcher.InvokeAsync(() => pendingDeletion is not null ? new DockPanel("画像を削除",
            "この元画像をごみ箱へ移します。すべてのモニターの履歴からも取り除きます。",
            new[] { new PanelFact("対象ファイル", pendingDeletion) }, new[] {
                HistoryAction("ごみ箱へ移す", "delete.confirm"),
                HistoryAction("キャンセル", "delete.cancel"),
            }) : historyVisible ? new DockPanel("最近使った壁紙",
            $"モニター{historyMonitor + 1}: {historyTotal}件 / {historyPage + 1}ページ目。履歴は実行中だけ保持します。",
            Facts: Array.Empty<PanelFact>(), Actions: new[] {
                new PanelAction("前のページ", services.ExtensionId + ".history.previous"),
                new PanelAction("次のページ", services.ExtensionId + ".history.next"),
                new PanelAction("履歴を更新", services.ExtensionId + ".history"),
                new PanelAction("壁紙の操作に戻る", services.ExtensionId + ".home"),
            }) { Images = historyImages, Tabs = Enumerable.Range(0, HistoryManager.Instance.MonitorCount)
                .Select(monitor => HistoryAction($"モニター{monitor + 1}" + (monitor == historyMonitor ? "（表示中）" : ""), "monitor." + monitor) with { Selected = monitor == historyMonitor }).ToArray() } : new DockPanel("壁紙スライドショー",
            error is null ? "モニターごとの画像フォルダーから、重複なしで壁紙を切り替えます。" : "設定エラー: " + error,
            new[] {
                new PanelFact("状態", engine.IsPaused ? "停止中（手動停止・ロック・リモート接続）" : "再生中"),
                new PanelFact("更新間隔", $"{config?.IntervalSeconds ?? 60} 秒"),
                new PanelFact("モニター", string.Join(" / ", StableScreensProvider.Screens.Select((s, i) => $"{i + 1}: {s.Bounds.Width}×{s.Bounds.Height}"))),
                new PanelFact("画像ソース", string.Join(" / ", config?.Monitors.Select((m, i) => $"{i + 1}: {m.SourceFolders().Count()}フォルダー") ?? [])),
                new PanelFact("バージョン", typeof(WallpaperApplet).Assembly.GetName().Version!.ToString(3)),
            }, new[] {
                new PanelAction("開始／再開", services.ExtensionId + ".start"),
                new PanelAction("停止", services.ExtensionId + ".stop"),
                new PanelAction("開始／停止を切り替え", services.ExtensionId + ".toggle"),
                new PanelAction("次の壁紙に更新", services.ExtensionId + ".next"),
                new PanelAction("最近使った壁紙", services.ExtensionId + ".history"),
                new PanelAction("データフォルダーを開く", services.ExtensionId + ".data"),
            }), token);
        await services.Ui.ShowPanelAsync(panel, token);
    }
    private async Task ShowHistoryAsync(int page, CancellationToken token)
    {
        pendingDeletion = null;
        historyGeneration = Guid.NewGuid().ToString("N");
        var pageSize = Math.Clamp(context?.Settings.Get("historyPageSize", 4) ?? 4, 1, MaximumPageSize);
        loadedPageSize = pageSize;
        var entries = await dispatcher.InvokeAsync(() => {
            historyMonitor = Math.Clamp(historyMonitor, 0, Math.Max(0, HistoryManager.Instance.MonitorCount - 1));
            var all = HistoryManager.Instance.Snapshot().Where(entry => entry.Monitor == historyMonitor).ToArray();
            historyTotal = all.Length;
            historyPage = Math.Clamp(page, 0, Math.Max(0, (all.Length - 1) / pageSize));
            monitorPages[historyMonitor] = historyPage;
            return all.Skip(historyPage * pageSize).Take(pageSize).ToArray();
        }, token);
        var size = new Size(config?.History.ThumbnailWidth ?? 480, config?.History.ThumbnailHeight ?? 360);
        if (context is null) throw new InvalidOperationException("Appletは停止しています。");
        var nextFiles = new List<string>();
        PanelImage[] images;
        try { images = await Task.Run(() => entries.Select((entry, i) => {
            token.ThrowIfCancellationRequested();
            using var thumbnail = ThumbnailResult.Load(entry.Path, size, token);
            string? file = null;
            if (thumbnail.Image is not null) {
                file = Path.Combine(imageFolder!, Guid.NewGuid().ToString("N") + ".png");
                nextFiles.Add(file);
                thumbnail.Image.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            }
            var name = Path.GetFileName(entry.Path);
            var maximum = config?.History.MaxFileNameLength ?? 30;
            if (name.Length > maximum) name = name[..maximum] + "…";
            return new PanelImage($"モニター{entry.Monitor + 1}: {name}",
                $"{thumbnail.Resolution} / {thumbnail.SizeText}", null,
                new[] { HistoryAction("画像を開く", "open." + i),
                    HistoryAction("画像を削除", "delete." + i) }) { Tooltip = entry.Path, ImageFile = file };
        }).ToArray(), token); }
        catch { foreach (var file in nextFiles) TryDelete(file); throw; }
        if (token.IsCancellationRequested) { foreach (var file in nextFiles) TryDelete(file); token.ThrowIfCancellationRequested(); }
        ClearImages(); imageFiles = nextFiles.ToArray();
        visibleHistory = entries; historyImages = images; historyVisible = true;
        await PublishAsync(token);
    }
    private PanelAction HistoryAction(string title, string action) => new(title, "") {
        ActionId = context!.ExtensionId + "." + historyGeneration + "." + action
    };
    public async Task HandlePanelActionAsync(string actionId, CancellationToken token) {
        await commandGate.WaitAsync(token);
        try {
            var prefix = context?.ExtensionId + "." + historyGeneration + ".";
            if (!historyVisible || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("履歴を更新してください。");
            var action = actionId[prefix.Length..];
            if (action == "delete.confirm") {
                var path = pendingDeletion ?? throw new InvalidOperationException("履歴から削除する画像を選んでください。");
                await Task.Run(() => { token.ThrowIfCancellationRequested(); if (File.Exists(path)) (deleteImage ?? ImageDeletion.Recycle)(path); }, token);
                await dispatcher.InvokeAsync(() => HistoryManager.Instance.Remove(path), token);
                pendingDeletion = null;
                await ShowHistoryAsync(historyPage, token);
            } else if (action == "delete.cancel") await ShowHistoryAsync(historyPage, token);
            else {
                var parts = action.Split('.');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var slot) || slot < 0) throw new InvalidOperationException("操作が不正です。");
                if (parts[0] == "monitor") { await SelectMonitorAsync(slot, token); return; }
                if (slot >= visibleHistory.Length) throw new InvalidOperationException("履歴を更新してください。");
                var path = visibleHistory[slot].Path;
                if (parts[0] == "open") await dispatcher.InvokeAsync(() => {
                    if (!File.Exists(path)) throw new FileNotFoundException("画像が見つかりません。", path);
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }, token);
                else if (parts[0] == "delete") { pendingDeletion = path; await PublishAsync(token); }
                else throw new InvalidOperationException("操作が不正です。");
            }
        } finally { commandGate.Release(); }
    }
    private Task SelectMonitorAsync(int monitor, CancellationToken token) {
        historyMonitor = Math.Clamp(monitor, 0, Math.Max(0, HistoryManager.Instance.MonitorCount - 1));
        return ShowHistoryAsync(monitorPages.GetValueOrDefault(historyMonitor), token);
    }
    private static void TryDelete(string file) {
        try { File.Delete(file); } catch (Exception error) { Console.Error.WriteLine(error.Message); }
    }
    private void ClearImages() { foreach (var file in imageFiles) TryDelete(file); imageFiles = []; }
    private async void DisplayChanged(object? sender, EventArgs e) => await RefreshStateAsync();
    private async void StateChanged() => await RefreshStateAsync();
    private async Task RefreshStateAsync() {
        try { await PublishAsync(CancellationToken.None); }
        catch (Exception error) { Console.Error.WriteLine(error); }
    }
    public async Task DeactivateAsync(CancellationToken token)
    {
        subscription?.Dispose(); subscription = null;
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        ApplicationController.Instance.StateChanged -= StateChanged;
        context = null;
        await dispatcher.InvokeAsync(() => {
            pendingDeletion = null; historyImages = []; visibleHistory = []; historyVisible = false;
            ClearImages();
            if (imageFolder is not null) {
                try { Directory.Delete(imageFolder); } catch (Exception error) { Console.Error.WriteLine(error.Message); }
                imageFolder = null;
            }
            if (started) { engine.Stop(); started = false; }
        }, CancellationToken.None);
    }
}
