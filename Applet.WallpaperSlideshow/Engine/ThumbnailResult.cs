namespace at365.WallpaperSlideshow;

internal sealed class ThumbnailResult : IDisposable
{
    private static readonly SemaphoreSlim Slots = new(2);
    public Image? Image { get; private set; }
    public string SizeText { get; private init; } = "";
    public string Resolution { get; private init; } = "";

    public static ThumbnailResult Load(string path, Size size, CancellationToken token)
    {
        Slots.Wait(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var image = ImageLoader.Load(path, size, out var original);
            string sizeText = "(読み込めません)";
            try { sizeText = $"{new FileInfo(path).Length / 1024f / 1024f:0.00} MB"; }
            catch (Exception ex) { AppLog.Error("サムネイル情報: " + path, ex); }
            return new ThumbnailResult
            {
                Image = image, SizeText = sizeText,
                Resolution = original.IsEmpty ? "" : $"{original.Width}×{original.Height}"
            };
        }
        finally { Slots.Release(); }
    }

    public Image? TakeImage()
    {
        var image = Image;
        Image = null;
        return image;
    }

    public void Dispose() => Image?.Dispose();
}
