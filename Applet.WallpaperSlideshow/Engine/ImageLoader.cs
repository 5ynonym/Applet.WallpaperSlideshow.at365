using System.Drawing.Imaging;

namespace at365.WallpaperSlideshow;

internal static class ImageLoader
{
    public static Size? ReadSize(string path)
    {
        try {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var image = Image.FromStream(stream);
            return image.Size;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or OutOfMemoryException or System.Runtime.InteropServices.ExternalException) {
            AppLog.Error("画像サイズ: " + path, ex); return null;
        }
    }
    // Materialize pixels while both the decoder and its stream are still alive.
    public static Bitmap? Load(string path, Size? maximumSize = null)
        => Load(path, maximumSize, out _);

    public static Bitmap? Load(string path, Size? maximumSize, out Size originalSize)
    {
        originalSize = Size.Empty;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var source = Image.FromStream(stream);
            originalSize = source.Size;
            var size = source.Size;
            if (maximumSize is { } maximum)
            {
                var scale = Math.Min(1d, Math.Min((double)maximum.Width / size.Width,
                    (double)maximum.Height / size.Height));
                size = new Size(Math.Max(1, (int)(size.Width * scale)),
                    Math.Max(1, (int)(size.Height * scale)));
            }

            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            try
            {
                using var graphics = Graphics.FromImage(bitmap);
                graphics.DrawImage(source, new Rectangle(Point.Empty, size));
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
        {
            AppLog.Error("画像読み込み: " + path, ex);
            return null;
        }
    }
}
