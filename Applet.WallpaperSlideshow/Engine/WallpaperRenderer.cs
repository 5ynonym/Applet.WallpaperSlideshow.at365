using System.Drawing.Imaging;
using Image = System.Drawing.Image;

namespace at365.WallpaperSlideshow
{
    public sealed class WallpaperRenderer
    {
        public static WallpaperRenderer Instance => _lazy.Value;
        private static readonly Lazy<WallpaperRenderer> _lazy = new(() => new WallpaperRenderer());

        private Config _config = new(); // Initialize to avoid null reference
        private static readonly Bitmap _empty = new(1, 1, PixelFormat.Format1bppIndexed);

        internal WallpaperRenderer() { }
        internal CancellationToken Cancellation { get; set; }

        public void SetConfig(Config config)
        {
            _config = config ?? new Config();
        }

        /// <summary>
        /// Loads an image from the specified path without locking the file.
        /// </summary>
        public static Image? LoadImageWithoutLock(string path)
        {
            return ImageLoader.Load(path);
        }

        /// <summary>
        /// Composes the wallpaper for a specific monitor.
        /// </summary>
        public void ComposeMonitor(
            int monitorIndex,
            string? monitorImage,
            Graphics gMain,
            Rectangle virtualBounds,
            Rectangle[] screens,
            Queue<string> queue,
            Action<int, string> pushHistory)
        {
            if (monitorIndex < 0 || monitorIndex >= screens.Length) return;

            var monitorConfig = GetMonitorConfig(monitorIndex);
            Cancellation.ThrowIfCancellationRequested();
            var bounds = screens[monitorIndex];

            var drawRect = CalculateDrawRectangle(bounds, virtualBounds, monitorConfig);
            if (drawRect.Width <= 0 || drawRect.Height <= 0) return;

            if (monitorImage != null && !File.Exists(monitorImage)) return;

            var state = gMain.Save();
            try
            {
                gMain.SetClip(drawRect, System.Drawing.Drawing2D.CombineMode.Intersect);
                var mode = monitorConfig.Mode ?? StretchMode.Fit;
                if (mode == StretchMode.Tile)
                    HandleTileMode(monitorIndex, monitorImage, gMain, drawRect, queue, monitorConfig, pushHistory);
                else
                    HandleSingleImageMode(monitorIndex, monitorImage, gMain, drawRect, mode, pushHistory);
            }
            finally
            {
                gMain.Restore(state);
            }
        }

        private MonitorConfig GetMonitorConfig(int monitorIndex)
        {
            return (monitorIndex < _config.Monitors.Count)
                ? _config.Monitors[monitorIndex]
                : new MonitorConfig();
        }

        private static Rectangle CalculateDrawRectangle(Rectangle bounds, Rectangle virtualBounds, MonitorConfig config)
        {
            return new Rectangle(
                bounds.Left - virtualBounds.Left + config.PaddingLeft,
                bounds.Top - virtualBounds.Top + config.PaddingTop,
                bounds.Width - config.PaddingLeft - config.PaddingRight,
                bounds.Height - config.PaddingTop - config.PaddingBottom
            );
        }

        private void HandleTileMode(
            int monitorIndex,
            string? monitorImage,
            Graphics gMain,
            Rectangle drawRect,
            Queue<string> queue,
            MonitorConfig config,
            Action<int, string> pushHistory)
        {
            var paths = CollectImagePaths(monitorImage, queue, config.TileCount);
            if (paths.Count == 0) return;

            var images = ReadImageSizes(paths);
            if (images.Count == 0) return;

            DrawTile(gMain, images.ToArray(), drawRect, path => pushHistory(monitorIndex, path));
        }

        private static List<string> CollectImagePaths(string? primaryImage, Queue<string> queue, int tileCount)
        {
            var paths = new List<string>();
            if (primaryImage != null && File.Exists(primaryImage))
            {
                paths.Add(primaryImage);
            }

            while (paths.Count < tileCount && queue.Count > 0)
            {
                var next = queue.Dequeue();
                if (next != null && File.Exists(next))
                {
                    paths.Add(next);
                }
            }

            return paths;
        }

        private sealed record TileImage(string Path, int Width, int Height);
        private List<TileImage> ReadImageSizes(List<string> paths)
        {
            var images = new List<TileImage>();
            foreach (var path in paths)
            {
                Cancellation.ThrowIfCancellationRequested();
                if (ImageLoader.ReadSize(path) is { } size)
                    images.Add(new TileImage(path, size.Width, size.Height));
            }
            return images;
        }
        private void HandleSingleImageMode(
            int monitorIndex,
            string? monitorImage,
            Graphics gMain,
            Rectangle drawRect,
            StretchMode mode,
            Action<int, string> pushHistory)
        {
            if (monitorImage == null) return;

            using var img = ImageLoader.Load(monitorImage, mode == StretchMode.Fit ? drawRect.Size : null);
            if (img == null) return;

            DrawImageWithMode(gMain, img, drawRect, mode);
            pushHistory(monitorIndex, monitorImage);
        }

        /// <summary>
        /// Draws an image with the specified stretch mode.
        /// </summary>
        public void DrawImageWithMode(Graphics g, Image img, Rectangle drawRect, StretchMode mode)
        {
            if (drawRect.Width <= 0 || drawRect.Height <= 0) return;
            var state = g.Save();
            try
            {
                g.SetClip(drawRect, System.Drawing.Drawing2D.CombineMode.Intersect);
                switch (mode)
                {
                    case StretchMode.Fill:
                        DrawScaledImage(g, img, drawRect, Math.Max);
                        break;
                    case StretchMode.Fit:
                        DrawScaledImage(g, img, drawRect, Math.Min);
                        break;
                    case StretchMode.Stretch:
                        g.DrawImage(img, drawRect);
                        break;
                    case StretchMode.Center:
                        DrawCenteredImage(g, img, drawRect);
                        break;
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private static void DrawScaledImage(Graphics g, Image img, Rectangle drawRect, Func<float, float, float> scaleFunc)
        {
            float scale = scaleFunc((float)drawRect.Width / img.Width, (float)drawRect.Height / img.Height);
            int w = (int)(img.Width * scale);
            int h = (int)(img.Height * scale);
            int x = drawRect.Left + (drawRect.Width - w) / 2;
            int y = drawRect.Top + (drawRect.Height - h) / 2;
            g.DrawImage(img, new Rectangle(x, y, w, h));
        }

        private static void DrawCenteredImage(Graphics g, Image img, Rectangle drawRect)
        {
            int x = drawRect.Left + (drawRect.Width - img.Width) / 2;
            int y = drawRect.Top + (drawRect.Height - img.Height) / 2;
            g.DrawImage(img, new Rectangle(x, y, img.Width, img.Height));
        }

        /// <summary>
        /// Draws images in a tiled layout.
        /// </summary>
        private void DrawTile(Graphics g, TileImage[] images, Rectangle rect, Action<string> pushHistory)
        {
            var layout = FindBestTileLayout(images, rect);
            if (layout == null) return;

            // Shuffle images in each row for variety
            foreach (var row in layout.Rows)
            {
                Shuffle(row);
            }

            float totalHeight = layout.RowHeights.Sum();
            float gapY = Math.Max(0, (rect.Height - totalHeight) / (layout.Rows.Length + 1));
            float y = rect.Top + gapY;

            for (int i = 0; i < layout.Rows.Length; i++)
            {
                DrawRowWithMargin(g, layout.Rows[i], rect, layout.RowHeights[i], y, pushHistory);
                y += layout.RowHeights[i] + gapY;
            }
        }

        private class TileLayout
        {
            public TileImage[][] Rows { get; set; } = Array.Empty<TileImage[]>();
            public float[] RowHeights { get; set; } = Array.Empty<float>();
        }

        private static TileLayout? FindBestTileLayout(TileImage[] images, Rectangle rect)
        {
            TileLayout? bestLayout = null;
            float bestScore = float.MaxValue;

            for (int rowCount = 1; rowCount <= images.Length; rowCount++)
            {
                var rows = SplitRowsAspectBalanced(images, rowCount);
                var heights = new float[rowCount];
                bool valid = true;

                for (int i = 0; i < rowCount; i++)
                {
                    heights[i] = CalcRowHeightNoOverlap(rows[i], rect.Width);
                    if (heights[i] <= 0)
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid) continue;

                float totalHeight = heights.Sum();
                if (totalHeight > rect.Height)
                {
                    float scale = rect.Height / totalHeight;
                    for (int i = 0; i < rowCount; i++)
                        heights[i] *= scale;
                    totalHeight = heights.Sum();
                }

                float unusedHeight = rect.Height - totalHeight;
                float unusedWidth = 0;
                for (int i = 0; i < rowCount; i++)
                {
                    float rowWidth = rows[i].Sum(im => (float)im.Width / im.Height * heights[i]);
                    unusedWidth += Math.Max(0, rect.Width - rowWidth);
                }

                float score = unusedHeight * 3f + unusedWidth;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestLayout = new TileLayout { Rows = rows, RowHeights = heights };
                }
            }

            return bestLayout;
        }

        private static TileImage[][] SplitRowsAspectBalanced(TileImage[] images, int rows)
        {
            var result = new List<TileImage>[rows];
            for (int i = 0; i < rows; i++)
                result[i] = new List<TileImage>();

            float[] rowAspects = new float[rows];

            foreach (var img in images.OrderByDescending(i => (float)i.Width / i.Height))
            {
                int target = Array.IndexOf(rowAspects, rowAspects.Min());
                result[target].Add(img);
                rowAspects[target] += (float)img.Width / img.Height;
            }

            return result.Select(r => r.ToArray()).ToArray();
        }

        private static float CalcRowHeightNoOverlap(TileImage[] row, int totalWidth)
        {
            if (row.Length == 0) return 0;
            float sumAspect = row.Sum(im => (float)im.Width / im.Height);
            float h = totalWidth / sumAspect;
            return AdjustHeight(row, totalWidth, h);
        }

        private static float AdjustHeight(TileImage[] row, int totalWidth, float h)
        {
            const int MAX_ITER = 200;

            for (int i = 0; i < MAX_ITER; i++)
            {
                float width = 0;
                foreach (var img in row)
                    width += (img.Width / (float)img.Height) * h;

                if (width <= totalWidth)
                    return h;

                h *= 0.95f;
                if (h < 1f)
                    return -1f;
            }

            return -1f;
        }

        private void DrawRowWithMargin(Graphics g, TileImage[] row, Rectangle rect, float rowHeight, float y, Action<string> pushHistory)
        {
            if (row.Length == 0) return;

            float[] widths = row.Select(im => (float)im.Width / im.Height * rowHeight).ToArray();
            float totalWidth = widths.Sum();
            float gapX = Math.Max(0, (rect.Width - totalWidth) / (row.Length + 1));

            float x = rect.Left + gapX;
            for (int i = 0; i < row.Length; i++)
            {
                float w = widths[i];
                var layoutRect = new RectangleF(x, y, w, rowHeight);
                float margin = _config.TileMargin / 2f;
                var inner = RectangleF.Inflate(layoutRect, -margin, -margin);
                if (inner.Width <= 0 || inner.Height <= 0) continue;

                Cancellation.ThrowIfCancellationRequested();
                var target = Rectangle.Round(inner);
                if (target.Width <= 0 || target.Height <= 0) continue;
                using var image = ImageLoader.Load(row[i].Path, target.Size);
                if (image != null) { DrawImageFit(g, image, target); pushHistory(row[i].Path); }
                x += w + gapX;
            }
        }

        private static void DrawImageFit(Graphics g, Image img, Rectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;

            float scale = Math.Min((float)rect.Width / img.Width, (float)rect.Height / img.Height);
            int w = (int)(img.Width * scale);
            int h = (int)(img.Height * scale);
            int x = rect.Left + (rect.Width - w) / 2;
            int y = rect.Top + (rect.Height - h) / 2;
            g.DrawImage(img, new Rectangle(x, y, w, h));
        }

        /// <summary>
        /// Overwrites the target file with a black image.
        /// </summary>
        public void OverwriteWithBlack(string targetPath)
        {
            try
            {
                _empty.Save(targetPath, ImageFormat.Bmp);
            }
            catch (Exception ex)
            {
                AppLog.Error("壁紙ファイルの黒塗り", ex);
            }
        }
        private static void Shuffle(TileImage[] array)        {            for (int i = array.Length - 1; i > 0; i--)            {                int j = Random.Shared.Next(i + 1);                (array[i], array[j]) = (array[j], array[i]);            }        }    }}
