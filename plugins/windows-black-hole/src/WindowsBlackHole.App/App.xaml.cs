using System.Windows;

namespace WindowsBlackHole.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var freezeForDiagnostics = e.Args.Contains(
            "--diagnostic-freeze-capture",
            StringComparer.OrdinalIgnoreCase);
        var window = new MainWindow(freezeForDiagnostics);
        if (e.Args.Contains("--diagnostic-taskbar", StringComparer.OrdinalIgnoreCase))
        {
            window.ShowInTaskbar = true;
        }

        MainWindow = window;
        window.Show();
    }
}
