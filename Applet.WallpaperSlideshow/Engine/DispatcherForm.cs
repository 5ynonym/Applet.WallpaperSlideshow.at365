using System.Runtime.InteropServices;
using System.ComponentModel;

public sealed class DispatcherForm : Form
{
    private static readonly Lazy<DispatcherForm> _lazy = new(() => new DispatcherForm());
    public static DispatcherForm Instance => _lazy.Value;

    private DispatcherForm()
    {
        this.ShowInTaskbar = false;
        this.Opacity = 0;
        this.WindowState = FormWindowState.Minimized;
        this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action? OnRdpConnect { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action? OnRdpDisconnect { get; set; }

    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_SESSION_REMOTE_CONNECT = 0x03;
    private const int WTS_SESSION_REMOTE_DISCONNECT = 0x04;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel) at365.WallpaperSlideshow.ApplicationController.Instance.PrepareShutdown();
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
}
