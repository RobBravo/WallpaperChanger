using System.Windows;
using WallpaperChanger.App.Interop;
using WallpaperChanger.App.Services;
using WallpaperChanger.App.ViewModels;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Services;

namespace WallpaperChanger.App;

public partial class App : Application
{
    private TrayIconService? trayIconService;

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
            () => new ShuffleBagImagePicker(Random.Shared),
            new WindowsFolderPicker());

        MainWindow = new MainWindow
        {
            DataContext = viewModel
        };
        trayIconService = new TrayIconService(ShowMainWindow, ExitApplication);
        MainWindow.Show();

        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"WallpaperChanger could not load settings: {ex.Message}",
                "WallpaperChanger",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayIconService?.Dispose();
        trayIconService = null;

        base.OnExit(e);
    }

    private void ShowMainWindow()
    {
        if (MainWindow is MainWindow window)
        {
            window.ShowFromTray();
        }
    }

    private void ExitApplication()
    {
        if (MainWindow is MainWindow window)
        {
            window.AllowClose = true;
            window.Close();
        }

        Shutdown();
    }
}
