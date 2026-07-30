using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using WallpaperChanger.App.Services;
using WallpaperChanger.App.ViewModels;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Services;

namespace WallpaperChanger.App;

public partial class App : System.Windows.Application
{
    private MainViewModel? viewModel;
    private TrayIconService? trayIconService;
    private WallpaperRotationService? rotationService;
    private bool isRefreshingMonitors;

    protected override async void OnStartup(StartupEventArgs e)
    {
        SetThreadDpiAwarenessContext(new IntPtr(-4));

        base.OnStartup(e);

        var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to determine the application path.");
        var startupService = new StartupService(new CurrentUserRunKeyWriter(), "WallpaperChanger", executablePath);
        try
        {
            startupService.EnsureRegistered();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"WallpaperChanger could not register startup: {ex.Message}",
                "WallpaperChanger",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperChanger",
            "settings.json");
        var uiStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperChanger",
            "ui-state.json");

        var monitorRegistry = new WindowsMonitorRegistry();
        viewModel = new MainViewModel(
            new JsonSettingsStore(settingsPath),
            monitorRegistry,
            new CompositeWallpaperService(
                monitorRegistry,
                new CompositeWallpaperRenderer(),
                new SystemWallpaperGateway()),
            profile => new ShuffleBagImagePicker(Random.Shared, profile.RemainingImages),
            new WindowsFolderPicker(),
            new JsonUiStateStore(uiStatePath));

        MainWindow = new MainWindow
        {
            DataContext = viewModel
        };
        trayIconService = new TrayIconService(ShowMainWindow, ExitApplicationAsync);

        rotationService = new WallpaperRotationService(viewModel, new SystemClock());

        try
        {
            await viewModel.InitializeAsync();
            rotationService.Start();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
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

        if (isRefreshingMonitors)
        {
            return;
        }

        isRefreshingMonitors = true;

        try
        {
            var operation = Dispatcher.InvokeAsync(() => viewModel.RefreshAfterDisplayChangeAsync());
            var refreshTask = await operation.Task;
            await refreshTask;
        }
        catch (Exception ex)
        {
            viewModel.ReportError(ex);
        }
        finally
        {
            isRefreshingMonitors = false;
        }
    }

    private void ShowMainWindow()
    {
        if (MainWindow is MainWindow window)
        {
            window.ShowFromTray();
        }
    }

    private async Task ExitApplicationAsync()
    {
        try
        {
            if (viewModel is not null)
            {
                await viewModel.PersistAsync();
            }
        }
        catch (Exception ex)
        {
            viewModel?.ReportError(ex);
        }

        if (MainWindow is MainWindow window)
        {
            window.AllowClose = true;
            window.Close();
        }

        Shutdown();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
}
