using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using at365.WallpaperSlideshow;

internal static class SessionShutdownTests
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int maximum);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessShutdownParameters(out uint level, out uint flags);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessShutdownParameters(uint level, uint flags);

    public static void Find(string process)
    {
        var target = uint.Parse(process);
        var windows = new List<IntPtr>();
        var observed = new List<string>();
        EnumWindows((window, _) => {
            GetWindowThreadProcessId(window, out var actual);
            var name = new StringBuilder(256);
            if (actual == target && GetClassName(window, name, name.Capacity) != 0) {
                var style = GetWindowLong(window, -20);
                observed.Add($"{window.ToInt64()} {name} exStyle={style:x}");
                if (name.ToString().StartsWith("WindowsForms10.Window.", StringComparison.Ordinal)
                    && (style & 0x80080) == 0x80080) windows.Add(window); // Transparent tool form, excluding WinForms' helper HWND.
            }
            return true;
        }, IntPtr.Zero);
        if (windows.Count != 1) throw new Exception("Expected exactly one isolated DispatcherForm tool window: " + string.Join(" / ", observed));
        Console.WriteLine(windows[0].ToInt64());
    }

    public static void Send(string process, string handle, string action)
    {
        var window = new IntPtr(long.Parse(handle));
        GetWindowThreadProcessId(window, out var actual);
        if (actual != uint.Parse(process)) throw new InvalidOperationException("Fixture process/window mismatch");
        var (message, confirmed, flags) = action switch {
            "query" => (0x11u, 0, 0L),
            "cancel" => (0x16u, 0, 0L),
            "end" => (0x16u, 1, 0L),
            "logoff" => (0x16u, 1, 0x80000000L),
            _ => throw new InvalidOperationException("Unknown fixture message"),
        };
        if (SendMessageTimeout(window, message, (IntPtr)confirmed, (IntPtr)flags, 2, 3000, out var result) == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        Console.WriteLine(result.ToInt64());
    }

    public static void Run(string root)
    {
        if (!GetProcessShutdownParameters(out var previousLevel, out var previousFlags)) throw new Exception("Cannot read shutdown level");
        var form = DispatcherForm.Instance;
        _ = form.Handle;
        int cleanups = 0;
        int desktopCalls = 0;
        try
        {
            if (!GetProcessShutdownParameters(out var level, out _) || level != 0x2ff) throw new Exception("Native Applet must clean up before the host's default level");
            using (var image = new Bitmap(12, 8)) {
                using (var graphics = Graphics.FromImage(image)) graphics.Clear(Color.Red);
                image.Save(Const.WallpaperPicturePath);
            }
            form.OnSessionEnding = () => {
                cleanups++;
                WallpaperController.ClearWallpaper(path => {
                    desktopCalls++;
                    using var black = new Bitmap(Const.WallpaperPicturePath);
                    if (path != string.Empty || black.Size != new Size(1, 1) || black.GetPixel(0, 0).ToArgb() != Color.Black.ToArgb())
                        throw new Exception("Cleanup returned before clearing the generated BMP");
                });
            };
            Send(Environment.ProcessId.ToString(), form.Handle.ToInt64().ToString(), "query");
            Send(Environment.ProcessId.ToString(), form.Handle.ToInt64().ToString(), "cancel");
            if (cleanups != 0) throw new Exception("Cancelled session stopped playback");
            Send(Environment.ProcessId.ToString(), form.Handle.ToInt64().ToString(), "end");
            using (var black = new Bitmap(Const.WallpaperPicturePath)) {
                if (cleanups != 1 || desktopCalls != 1 || black.Size != new Size(1, 1) || black.GetPixel(0, 0).ToArgb() != Color.Black.ToArgb())
                    throw new Exception("WM_ENDSESSION did not synchronously clean up");
            }
            Send(Environment.ProcessId.ToString(), form.Handle.ToInt64().ToString(), "logoff");
            if (cleanups != 2) throw new Exception("Logoff did not use the same cleanup");
            Console.WriteLine("PASS: real hidden HWND / shutdown priority / query and cancellation / confirmed shutdown and logoff / synchronous black BMP cleanup with fake desktop API");
        }
        finally {
            form.OnSessionEnding = null;
            SetProcessShutdownParameters(previousLevel, previousFlags);
        }
    }
}
