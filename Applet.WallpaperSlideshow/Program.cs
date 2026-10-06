using System.Text;
using AppDock.Runtime;

namespace Applets.WallpaperSlideshow;

internal static class Program
{
    [STAThread]
    public static int Main()
    {
        if (!Console.IsInputRedirected) return 2;
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        var protocol = Console.Out;
        Console.SetOut(Console.Error);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        var dispatcher = DispatcherForm.Instance;
        _ = dispatcher.Handle;
        var applet = new WallpaperApplet(dispatcher, new SlideshowController(dispatcher));
        var exitCode = 0;
        Application.ThreadException += (_, e) => { Console.Error.WriteLine(e.Exception); exitCode = 1; Application.ExitThread(); };
        _ = Task.Run(async () => {
            try { await AppletSession.RunAsync(applet, Console.In, protocol); }
            catch (Exception error) { Console.Error.WriteLine(error); exitCode = 1; }
            finally { try { await dispatcher.InvokeAsync(Application.ExitThread); } catch (InvalidOperationException) { } }
        });
        Application.Run(new ApplicationContext());
        return exitCode;
    }
}
