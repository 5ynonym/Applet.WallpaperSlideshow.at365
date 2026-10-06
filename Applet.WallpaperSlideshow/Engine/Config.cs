using System.Text.Json;
using System.Text.Json.Serialization;

namespace at365.WallpaperSlideshow
{
    public enum StretchMode
    {
        Fill,    // 画面いっぱい（現在の動作）
        Fit,     // 黒帯ありで収まるように
        Stretch, // アスペクト比無視で引き伸ばし
        Center,   // 中央に等倍表示
        Tile,   // タイル表示 (TileCount枚数で画面を埋める)
    }

    public class MonitorConfig
    {
        public string? Folder { get; set; }
        public List<string> Folders { get; set; } = new();
        public IEnumerable<string> SourceFolders() => Folders.Append(Folder ?? "")
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        public StretchMode? Mode { get; set; } = StretchMode.Fit;
        public int TileCount { get; set; } = 4;
        public int PaddingLeft { get; set; } = 0;
        public int PaddingRight { get; set; } = 0;
        public int PaddingTop { get; set; } = 0;
        public int PaddingBottom { get; set; } = 0;
    }

    public class HistoryConfig
    {
        public int Limit { get; set; } = 30;
        public int ThumbnailWidth { get; set; } = 480;
        public int ThumbnailHeight { get; set; } = 360;
        public int MaxFileNameLength { get; set; } = 30;
    }

    public class Config
    {
        public int IntervalSeconds { get; set; } = 60;
        public HistoryConfig History { get; set; } = new HistoryConfig();
        public float TileMargin { get; set; } = 10;
        public List<MonitorConfig> Monitors { get; set; } = new();

        internal static Config Parse(string text)
        {
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            options.Converters.Add(new JsonStringEnumConverter());
            var config = JsonSerializer.Deserialize<Config>(text, options)
                ?? throw new InvalidDataException("設定に null は指定できません。");
            config.Validate();
            return config;
        }

        public void Validate(IEnumerable<Rectangle>? monitorBounds = null)
        {
            static void Range(int value, int minimum, int maximum, string name)
            {
                if (value < minimum || value > maximum)
                    throw new InvalidDataException($"{name} は {minimum}～{maximum} で指定してください。");
            }

            Range(IntervalSeconds, 1, int.MaxValue / 1000, nameof(IntervalSeconds));
            if (History == null || Monitors == null)
                throw new InvalidDataException("History と Monitors に null は指定できません。");
            if (Monitors.Count > 64) throw new InvalidDataException("Monitors は64件以内で指定してください。");
            Range(History.Limit, 0, 1000, "History.Limit");
            Range(History.ThumbnailWidth, 1, 2048, "History.ThumbnailWidth");
            Range(History.ThumbnailHeight, 1, 2048, "History.ThumbnailHeight");
            Range(History.MaxFileNameLength, 1, 1024, "History.MaxFileNameLength");
            if (!float.IsFinite(TileMargin) || TileMargin < 0 || TileMargin > 4096)
                throw new InvalidDataException("TileMargin は 0～4096 で指定してください。");

            var bounds = monitorBounds?.ToArray();
            for (int i = 0; i < Monitors.Count; i++)
            {
                var monitor = Monitors[i];
                if (monitor == null)
                    throw new InvalidDataException($"Monitors[{i}] に null は指定できません。");
                if (monitor.Folders == null || monitor.Folders.Count > 64 || monitor.Folders.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException($"Monitors[{i}].Folders は空文字を含まない64件以内のフォルダー配列です。");
                _ = monitor.SourceFolders().ToArray();
                if (monitor.Mode is { } mode && !Enum.IsDefined(mode))
                    throw new InvalidDataException($"Monitors[{i}].Mode が不正です。");
                Range(monitor.TileCount, 1, 64, $"Monitors[{i}].TileCount");
                Range(monitor.PaddingLeft, 0, 65535, $"Monitors[{i}].PaddingLeft");
                Range(monitor.PaddingRight, 0, 65535, $"Monitors[{i}].PaddingRight");
                Range(monitor.PaddingTop, 0, 65535, $"Monitors[{i}].PaddingTop");
                Range(monitor.PaddingBottom, 0, 65535, $"Monitors[{i}].PaddingBottom");
                if (bounds != null && i < bounds.Length &&
                    (monitor.PaddingLeft + monitor.PaddingRight >= bounds[i].Width ||
                     monitor.PaddingTop + monitor.PaddingBottom >= bounds[i].Height))
                    throw new InvalidDataException($"Monitors[{i}] の余白で描画領域がなくなります。");
            }
        }

    }
}
