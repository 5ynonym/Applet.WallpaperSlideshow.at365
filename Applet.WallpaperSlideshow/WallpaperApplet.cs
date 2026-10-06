using System.Diagnostics;
using AppDock.SDK;
using at365.WallpaperSlideshow;
using Microsoft.Win32;
using DockPanel = AppDock.SDK.Panel;

namespace Applets.WallpaperSlideshow;

internal sealed class WallpaperApplet(Form dispatcher, ISlideshowController engine) : IAppDockExtension
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
    public async Task ActivateAsync(IExtensionContext services, CancellationToken token)
    {
        context = services;
        var options = SlideshowOptions.Read(services.Settings);
        await dispatcher.InvokeAsync(() => {
            options.Validate(StableScreensProvider.Screens.Select(s => s.Bounds));
            started = true;
            engine.Start(options, services.Settings.Get("paused", false));
            config = options;
            fingerprint = SlideshowOptions.Fingerprint(options);
        }, token);
        services.Commands.Register(services.ExtensionId + ".start", "壁紙スライドショーを開始", ct => PauseAsync(false, ct));
        services.Commands.Register(services.ExtensionId + ".stop", "壁紙スライドショーを停止", ct => PauseAsync(true, ct));
        services.Commands.Register(services.ExtensionId + ".next", "次の壁紙に更新", async ct => {
            await dispatcher.InvokeAsync(engine.Next, ct); await PublishAsync(ct);
        });
        services.Commands.Register(services.ExtensionId + ".pause", "壁紙スライドショーを一時停止", ct => PauseAsync(true, ct));
        services.Commands.Register(services.ExtensionId + ".resume", "壁紙スライドショーを再開", ct => PauseAsync(false, ct));
        services.Commands.Register(services.ExtensionId + ".toggle", "壁紙スライドショーの開始／停止を切り替え", ct => PauseAsync(!services.Settings.Get("paused", false), ct));
        services.Commands.Register(services.ExtensionId + ".history", "最近使った壁紙を表示", ct => ShowHistoryAsync(0, ct));
        services.Commands.Register(services.ExtensionId + ".history.previous", "壁紙履歴の前のページ", ct => ShowHistoryAsync(historyPage - 1, ct));
        services.Commands.Register(services.ExtensionId + ".history.next", "壁紙履歴の次のページ", ct => ShowHistoryAsync(historyPage + 1, ct));
        services.Commands.Register(services.ExtensionId + ".home", "壁紙の操作に戻る", async ct => {
            historyVisible = false; historyImages = []; visibleHistory = []; await PublishAsync(ct);
        });
        for (int i = 0; i < 4; i++) {
            var slot = i;
            services.Commands.Register(services.ExtensionId + ".history.open." + slot, $"壁紙履歴{slot + 1}を開く", async ct => {
                await dispatcher.InvokeAsync(() => {
                    if (slot >= visibleHistory.Length) throw new InvalidOperationException("履歴を更新してください。");
                    var path = visibleHistory[slot].Path;
                    if (!File.Exists(path)) throw new FileNotFoundException("画像が見つかりません。", path);
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }, ct);
            });
        }
        services.Commands.Register(services.ExtensionId + ".data", "壁紙のデータフォルダーを開く", async ct => {
            await dispatcher.InvokeAsync(() => {
                Directory.CreateDirectory(Const.AppDataFolder);
                Process.Start(new ProcessStartInfo(Const.AppDataFolder) { UseShellExecute = true });
            }, ct);
        });
        subscription = services.Settings.OnChanged(ApplyAsync);
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
            await dispatcher.InvokeAsync(() => {
                options.Validate(StableScreensProvider.Screens.Select(s => s.Bounds));
                var nextFingerprint = SlideshowOptions.Fingerprint(options);
                if (nextFingerprint != fingerprint) { engine.Configure(options); fingerprint = nextFingerprint; config = options; }
                engine.Pause(services.Settings.Get("paused", false));
            }, token);
            await PublishAsync(token);
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
        var panel = await dispatcher.InvokeAsync(() => historyVisible ? new DockPanel("最近使った壁紙",
            $"{historyTotal}件 / {historyPage + 1}ページ目。履歴は実行中だけ保持します。",
            Facts: Array.Empty<PanelFact>(), Actions: new[] {
                new PanelAction("前のページ", services.ExtensionId + ".history.previous"),
                new PanelAction("次のページ", services.ExtensionId + ".history.next"),
                new PanelAction("履歴を更新", services.ExtensionId + ".history"),
                new PanelAction("壁紙の操作に戻る", services.ExtensionId + ".home"),
            }) { Images = historyImages } : new DockPanel("壁紙スライドショー",
            error is null ? "モニターごとの画像フォルダーから、重複なしで壁紙を切り替えます。" : "設定エラー: " + error,
            new[] {
                new PanelFact("状態", engine.IsPaused ? "停止中（手動停止・ロック・リモート接続）" : "再生中"),
                new PanelFact("更新間隔", $"{config?.IntervalSeconds ?? 60} 秒"),
                new PanelFact("モニター", string.Join(" / ", StableScreensProvider.Screens.Select((s, i) => $"{i + 1}: {s.Bounds.Width}×{s.Bounds.Height}"))),
                new PanelFact("画像ソース", string.Join(" / ", config?.Monitors.Select((m, i) => $"{i + 1}: {m.SourceFolders().Count()}フォルダー") ?? [])),
                new PanelFact("バージョン", typeof(WallpaperApplet).Assembly.GetName().Version!.ToString(3)),
            }, new[] {
                new PanelAction("開始", services.ExtensionId + ".start"),
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
        var entries = await dispatcher.InvokeAsync(() => {
            var all = HistoryManager.Instance.Snapshot();
            historyTotal = all.Length;
            historyPage = Math.Clamp(page, 0, Math.Max(0, (all.Length - 1) / 4));
            return all.Skip(historyPage * 4).Take(4).ToArray();
        }, token);
        var size = new Size(Math.Min(config?.History.ThumbnailWidth ?? 320, 320), Math.Min(config?.History.ThumbnailHeight ?? 240, 240));
        var extensionId = context?.ExtensionId ?? throw new InvalidOperationException("Appletは停止しています。");
        var images = await Task.Run(() => entries.Select((entry, i) => {
            token.ThrowIfCancellationRequested();
            using var thumbnail = ThumbnailResult.Load(entry.Path, size, token);
            string? image = null;
            if (thumbnail.Image is not null) {
                using var stream = new MemoryStream();
                thumbnail.Image.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                var encoded = "data:image/jpeg;base64," + Convert.ToBase64String(stream.ToArray());
                if (encoded.Length <= 200000) image = encoded;
            }
            var name = Path.GetFileName(entry.Path);
            var maximum = Math.Min(config?.History.MaxFileNameLength ?? 30, 160);
            if (name.Length > maximum) name = name[..maximum] + "…";
            return new PanelImage($"モニター{entry.Monitor + 1}: {name}",
                $"{thumbnail.Resolution} / {thumbnail.SizeText}", image,
                new[] { new PanelAction("画像を開く", extensionId + ".history.open." + i) });
        }).ToArray(), token);
        token.ThrowIfCancellationRequested();
        visibleHistory = entries; historyImages = images; historyVisible = true;
        await PublishAsync(token);
    }
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
            historyImages = []; visibleHistory = []; historyVisible = false;
            if (started) { engine.Stop(); started = false; }
        }, CancellationToken.None);
    }
}
