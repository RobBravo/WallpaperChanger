using System.Windows;
using WallpaperChanger.App.Services;

namespace WallpaperChanger.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to determine the application path.");
        var startupService = new StartupService(new CurrentUserRunKeyWriter(), "WallpaperChanger", executablePath);
        startupService.EnsureRegistered();

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
