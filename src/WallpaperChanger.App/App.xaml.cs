using System.Windows;
using WallpaperChanger.App.Interop;
using WallpaperChanger.App.Services;
using WallpaperChanger.App.ViewModels;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Services;

namespace WallpaperChanger.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to determine the application path.");
        var startupService = new StartupService(new CurrentUserRunKeyWriter(), "WallpaperChanger", executablePath);
        startupService.EnsureRegistered();

        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperChanger",
            "settings.json");

        var viewModel = new MainViewModel(
            new JsonSettingsStore(settingsPath),
            new WindowsMonitorRegistry(),
            new DesktopWallpaperService(new DesktopWallpaper()),
            new ShuffleBagImagePicker(Random.Shared),
            new WindowsFolderPicker());

        await viewModel.InitializeAsync();

        MainWindow = new MainWindow
        {
            DataContext = viewModel
        };
        MainWindow.Show();
    }
}
