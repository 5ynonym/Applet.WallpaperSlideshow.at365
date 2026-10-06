using System.Text.Json;
using AppDock.SDK;
using at365.WallpaperSlideshow;

namespace Applets.WallpaperSlideshow;

internal static class SlideshowOptions
{
    public static Config Read(ISettingsService settings)
    {
        var monitors = settings.Get("monitors", JsonSerializer.SerializeToElement(Array.Empty<object>()));
        var text = monitors.ValueKind == JsonValueKind.String ? monitors.GetString()! : monitors.GetRawText();
        var config = Config.Parse("{\"Monitors\":" + text + "}");
        config.IntervalSeconds = settings.Get("intervalSeconds", 60);
        config.TileMargin = settings.Get("tileMargin", 10f);
        config.History = new HistoryConfig {
            Limit = settings.Get("historyLimit", 30),
            ThumbnailWidth = settings.Get("thumbnailWidth", 480),
            ThumbnailHeight = settings.Get("thumbnailHeight", 360),
            MaxFileNameLength = settings.Get("maxFileNameLength", 30),
        };
        config.Validate();
        return config;
    }
    public static string Fingerprint(Config config) => JsonSerializer.Serialize(config);
}
