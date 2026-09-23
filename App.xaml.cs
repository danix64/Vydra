using System.IO;
using System.Windows;
using Vydra.Services;

namespace Vydra;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string toolsDir = Path.Combine(AppContext.BaseDirectory, "Tools");
        var tools = new ToolManager(toolsDir);

        var missing = tools.GetMissingTools();
        if (missing.Count > 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var startup = new StartupWindow(tools);
            startup.ShowDialog();

            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }

        var main = new MainWindow();
        main.Show();
    }
}