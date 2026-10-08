using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Applets.WallpaperSlideshow;

internal interface IWindowsBackgroundApi : IDisposable
{
    void SetPicture(string path);
    void SetSpan();
    bool IsPicture { get; }
    bool IsSpan { get; }
}

internal static class WindowsBackground
{
    internal const string ManualInstructions = "手動では Windows の「設定」→「個人用設定」→「背景」で、背景を「画像」、画像の調整を「スパン」にしてください。";

    public static void Apply(bool paused)
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
        Apply(paused, cache, at365.WallpaperSlideshow.Const.AppDataFolder, () => new DesktopBackgroundApi());
    }

    // The slideshow's wallpaper.bmp is blackened after publication. Preserve the shell's
    // cached display image instead, and keep the copied image available to Windows.
    internal static void Apply(bool paused, string cache, string directory, Func<IWindowsBackgroundApi> createApi)
    {
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "windows-background.bmp");
        var staging = Path.Combine(directory, $"background-{Guid.NewGuid():N}.bmp");
        try
        {
            if (paused)
            {
                using var image = new Bitmap(1, 1);
                image.SetPixel(0, 0, Color.Black);
                image.Save(staging, ImageFormat.Bmp);
            }
            else
            {
                if (!File.Exists(cache))
                    throw new InvalidOperationException("現在の壁紙を取得できません。壁紙の表示を待つか、手動で設定してください。");
                using var stream = new FileStream(cache, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var image = Image.FromStream(stream);
                image.Save(staging, ImageFormat.Bmp);
            }
            File.Move(staging, output, true);
            using var api = createApi();
            api.SetPicture(output);
            api.SetSpan();
            if (!api.IsPicture || !api.IsSpan)
                throw new InvalidOperationException("Windowsの背景が「画像・スパン」になったことを確認できません。" + ManualInstructions);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }

    internal sealed class DesktopBackgroundApi : IWindowsBackgroundApi
    {
        private readonly IDesktopWallpaper wallpaper = (IDesktopWallpaper)Activator.CreateInstance(
            Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"), true)!)!;
        public void SetPicture(string path)
        {
            // Windows Settings has no public background-kind setter. Store the user's
            // Picture preference, then let the shell apply the image and stop its slideshow.
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers");
            key.SetValue("BackgroundType", 0, RegistryValueKind.DWord);
            wallpaper.SetWallpaper(null, path);
        }
        public void SetSpan() => wallpaper.SetPosition(5);
        public bool IsSpan { get { wallpaper.GetPosition(out var position); return position == 5; } }
        public bool IsPicture
        {
            get
            {
                wallpaper.GetStatus(out var status);
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers");
                return key?.GetValue("BackgroundType") is int kind && kind == 0 && (status & 2) == 0;
            }
        }
        public void Dispose() => Marshal.ReleaseComObject(wallpaper);
    }

    // Preserve the native vtable order, including the unused methods before GetStatus.
    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitor, [MarshalAs(UnmanagedType.LPWStr)] string path);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitor, [MarshalAs(UnmanagedType.LPWStr)] out string path);
        void GetMonitorDevicePathAt(uint index, [MarshalAs(UnmanagedType.LPWStr)] out string monitor);
        void GetMonitorDevicePathCount(out uint count);
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor, out NativeRect rect);
        void SetBackgroundColor(uint color);
        void GetBackgroundColor(out uint color);
        void SetPosition(uint position);
        void GetPosition(out uint position);
        void SetSlideshow(IntPtr items);
        void GetSlideshow(out IntPtr items);
        void SetSlideshowOptions(uint options, uint milliseconds);
        void GetSlideshowOptions(out uint options, out uint milliseconds);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitor, uint direction);
        void GetStatus(out uint status);
        void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
}
