using System.Windows;
using Microsoft.Win32;
using WallpaperChanger.App.Interop;
using WallpaperChanger.App.Services;
using WallpaperChanger.App.ViewModels;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Services;

namespace WallpaperChanger.App;

public partial class App : Application
{
    private MainViewModel? viewModel;
    private TrayIconService? trayIconService;
    private WallpaperRotationService? rotationService;

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

        viewModel = new MainViewModel(
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

        rotationService = new WallpaperRotationService(viewModel, new SystemClock());
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        try
        {
            await viewModel.InitializeAsync();
            rotationService.Start();
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
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        rotationService?.Dispose();
        rotationService = null;

        trayIconService?.Dispose();
        trayIconService = null;

        base.OnExit(e);
    }

    private async void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (viewModel is null)
        {
            return;
        }

        try
        {
            var operation = Dispatcher.InvokeAsync(() => viewModel.InitializeAsync());
            var initializeTask = await operation.Task;
            await initializeTask;
        }
        catch (Exception ex)
        {
            viewModel.ReportError(ex);
        }
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
