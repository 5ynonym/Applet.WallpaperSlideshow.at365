using System.Runtime.InteropServices;
using System.ComponentModel;

public sealed class DispatcherForm : Form
{
    private static readonly Lazy<DispatcherForm> _lazy = new(() => new DispatcherForm());
    public static DispatcherForm Instance => _lazy.Value;

    private DispatcherForm()
    {
        // Run cleanup before the default shutdown level (0x280) of the parent host.
        if (!SetProcessShutdownParameters(0x2ff, 0))
            at365.WallpaperSlideshow.AppLog.Error("Windows終了順序の設定", new Win32Exception(Marshal.GetLastWin32Error()));
        this.ShowInTaskbar = false;
        this.Opacity = 0;
        this.WindowState = FormWindowState.Minimized;
        this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action? OnRdpConnect { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action? OnRdpDisconnect { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Action? OnSessionEnding { get; set; }

    private const int WM_QUERYENDSESSION = 0x0011;
    private const int WM_ENDSESSION = 0x0016;
    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_SESSION_REMOTE_CONNECT = 0x03;
    private const int WTS_SESSION_REMOTE_DISCONNECT = 0x04;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        // FormClosing with WindowsShutDown can occur during the query, which may be cancelled.
        if (!e.Cancel && e.CloseReason != CloseReason.WindowsShutDown) OnSessionEnding?.Invoke();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WTSRegisterSessionNotification(this.Handle, 0);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        WTSUnRegisterSessionNotification(this.Handle);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_QUERYENDSESSION)
        {
            m.Result = (IntPtr)1; // Allow shutdown; do not stop playback until it is confirmed.
            return;
        }
        if (m.Msg == WM_ENDSESSION)
        {
            if (m.WParam != IntPtr.Zero)
            {
                // Windows may terminate us as soon as this returns. Do not dispatch asynchronously.
                try { OnSessionEnding?.Invoke(); }
                catch (Exception error) { at365.WallpaperSlideshow.AppLog.Error("Windows終了時の壁紙クリーンアップ", error); }
            }
            m.Result = IntPtr.Zero;
            return;
        }
        if (m.Msg == WM_WTSSESSION_CHANGE)
        {
            int code = m.WParam.ToInt32();

            if (code == WTS_SESSION_REMOTE_CONNECT)
                OnRdpConnect?.Invoke();

            if (code == WTS_SESSION_REMOTE_DISCONNECT)
                OnRdpDisconnect?.Invoke();
        }

        base.WndProc(ref m);
    }

    [DllImport("wtsapi32.dll")]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int flags);

    [DllImport("wtsapi32.dll")]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessShutdownParameters(uint level, uint flags);
}
