using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Models;
using WallpaperChanger.Core.Services;

namespace WallpaperChanger.App.ViewModels;

public interface IFolderPicker
{
    string? PickFolder(string? initialFolder);
}

public sealed class MainViewModel : ObservableObject
{
    private readonly ISettingsStore settingsStore;
    private readonly IMonitorRegistry monitorRegistry;
    private readonly IWallpaperService wallpaperService;
    private readonly Func<WallpaperMonitorProfile, IImagePicker> imagePickerFactory;
    private readonly IFolderPicker folderPicker;
    private readonly Dictionary<string, WallpaperMonitorProfile> savedProfilesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly object stateLock = new();
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private readonly SemaphoreSlim snapshotApplyGate = new(1, 1);
    private string? statusMessage;
    private VirtualMonitorViewModel? selectedVirtualMonitor;

    public MainViewModel(
        ISettingsStore settingsStore,
        IMonitorRegistry monitorRegistry,
        IWallpaperService wallpaperService,
        Func<WallpaperMonitorProfile, IImagePicker> imagePickerFactory,
        IFolderPicker folderPicker)
    {
        this.settingsStore = settingsStore;
        this.monitorRegistry = monitorRegistry;
        this.wallpaperService = wallpaperService;
        this.imagePickerFactory = imagePickerFactory;
        this.folderPicker = folderPicker;
    }

    public ObservableCollection<MonitorRowViewModel> Monitors { get; } = new();

    public ObservableCollection<VirtualMonitorViewModel> VirtualMonitors { get; } = new();

    public VirtualMonitorViewModel? SelectedVirtualMonitor
    {
        get => selectedVirtualMonitor;
        set
        {
            if (value is null && VirtualMonitors.Count > 0)
            {
                value = VirtualMonitors[0];
            }

            SetProperty(ref selectedVirtualMonitor, value);
        }
    }

    public string? StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await snapshotApplyGate.WaitAsync(cancellationToken);
        try
        {
            await InitializeAsyncCore(cancellationToken);
        }
        finally
        {
            snapshotApplyGate.Release();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await snapshotApplyGate.WaitAsync(cancellationToken);
        try
        {
            await InitializeAsyncCore(cancellationToken);
            await RecomposeAsyncCore(cancellationToken);
        }
        finally
        {
            snapshotApplyGate.Release();
        }
    }

    public async Task RefreshAfterDisplayChangeAsync(CancellationToken cancellationToken = default)
    {
        await snapshotApplyGate.WaitAsync(cancellationToken);
        try
        {
            await SaveAsyncCore(cancellationToken);
            await InitializeAsyncCore(cancellationToken);
            await RecomposeAsyncCore(cancellationToken);
        }
        finally
        {
            snapshotApplyGate.Release();
        }
    }

    private async Task InitializeAsyncCore(CancellationToken cancellationToken)
    {
        var selectedMonitorId = SelectedVirtualMonitor?.MonitorId;
        IReadOnlyList<WallpaperMonitorProfile> savedProfiles;
        try
        {
            savedProfiles = await settingsStore.LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            savedProfiles = Array.Empty<WallpaperMonitorProfile>();
            SetStatusMessage($"Wallpaper settings could not be loaded: {ex.Message}");
        }
        savedProfilesById.Clear();

        foreach (var profile in savedProfiles)
        {
            if (string.IsNullOrWhiteSpace(profile.MonitorId))
            {
                continue;
            }

            savedProfilesById[profile.MonitorId] = profile;
        }

        Monitors.Clear();

        IReadOnlyList<MonitorDescriptor> monitors;
        try
        {
            monitors = monitorRegistry.GetConnectedMonitors();
        }
        catch (Exception ex)
        {
            monitors = Array.Empty<MonitorDescriptor>();
            SetStatusMessage($"Wallpaper monitors could not be detected: {ex.Message}");
        }

        UpdateVirtualMonitors(monitors, selectedMonitorId);

        foreach (var monitor in monitors)
        {
            var monitorId = monitor.Id;
            savedProfilesById.TryGetValue(monitorId, out var profile);
            var rowProfile = profile ?? new WallpaperMonitorProfile(monitorId);
            var row = new MonitorRowViewModel(this, rowProfile, imagePickerFactory(rowProfile));
            if (rowProfile.NextRunAt is { } nextRunAt && nextRunAt != DateTimeOffset.MaxValue && !string.IsNullOrWhiteSpace(rowProfile.FolderPath) && Directory.Exists(rowProfile.FolderPath))
            {
                row.RestoreNextRun(nextRunAt);
            }
            else
            {
                row.ScheduleNextRun();
            }
            Monitors.Add(row);
        }
    }

    private void UpdateVirtualMonitors(IReadOnlyList<MonitorDescriptor> monitors, string? selectedMonitorId)
    {
        VirtualMonitors.Clear();

        if (monitors.Count == 0)
        {
            SelectedVirtualMonitor = null;
            return;
        }

        var left = monitors.Min(monitor => monitor.Left);
        var top = monitors.Min(monitor => monitor.Top);
        var right = monitors.Max(monitor => monitor.Left + monitor.Width);
        var bottom = monitors.Max(monitor => monitor.Top + monitor.Height);
        var width = right - left;
        var height = bottom - top;

        foreach (var monitor in monitors.OrderBy(monitor => monitor.Left).ThenBy(monitor => monitor.Top))
        {
            VirtualMonitors.Add(new VirtualMonitorViewModel(
                monitor,
                (double)(monitor.Left - left) / width,
                (double)(monitor.Top - top) / height,
                (double)monitor.Width / width,
                (double)monitor.Height / height));
        }

        SelectedVirtualMonitor = VirtualMonitors.FirstOrDefault(monitor =>
            string.Equals(monitor.MonitorId, selectedMonitorId, StringComparison.OrdinalIgnoreCase))
            ?? VirtualMonitors[0];
    }

    public Task PersistAsync(CancellationToken cancellationToken = default)
    {
        return SaveAsync(cancellationToken);
    }

    public async Task RecomposeAsync(CancellationToken cancellationToken = default)
    {
        await snapshotApplyGate.WaitAsync(cancellationToken);
        try
        {
            await RecomposeAsyncCore(cancellationToken);
        }
        finally
        {
            snapshotApplyGate.Release();
        }
    }

    private Task RecomposeAsyncCore(CancellationToken cancellationToken)
    {
        var snapshot = Monitors
            .Where(monitor => !string.IsNullOrWhiteSpace(monitor.CurrentImagePath))
            .ToDictionary(monitor => monitor.MonitorId, monitor => monitor.CurrentImagePath!, StringComparer.OrdinalIgnoreCase);

        return snapshot.Count == 0
            ? Task.CompletedTask
            : wallpaperService.ApplyAsync(snapshot, cancellationToken);
    }

    private Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return SaveAsyncCore(cancellationToken);
    }

    private async Task SaveAsyncCore(CancellationToken cancellationToken)
    {
        await saveGate.WaitAsync(cancellationToken);

        try
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            var rows = dispatcher is null
                ? Monitors.ToArray()
                : await dispatcher.InvokeAsync(() => Monitors.ToArray());

            IReadOnlyCollection<WallpaperMonitorProfile> profiles;
            lock (stateLock)
            {
                var profilesById = new Dictionary<string, WallpaperMonitorProfile>(savedProfilesById, StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    profilesById[row.MonitorId] = row.ToProfile();
                }

                savedProfilesById.Clear();
                foreach (var pair in profilesById)
                {
                    savedProfilesById[pair.Key] = pair.Value;
                }

                profiles = profilesById.Values.ToArray();
            }

            await settingsStore.SaveAsync(profiles, cancellationToken);
        }
        finally
        {
            saveGate.Release();
        }
    }

    internal void BrowseFolder(MonitorRowViewModel row)
    {
        var selectedFolder = folderPicker.PickFolder(row.FolderPath);
        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            row.FolderPath = selectedFolder;
        }
    }

    internal void Reschedule(MonitorRowViewModel row)
    {
        row.ScheduleNextRun();
    }

    internal void ReportError(Exception exception)
    {
        SetStatusMessage(exception.Message);
    }

    internal async Task<bool> ApplyNowAsync(MonitorRowViewModel row, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(row.FolderPath) || !Directory.Exists(row.FolderPath))
        {
            row.RestoreNextRun(DateTimeOffset.MaxValue);
            SetStatusMessage($"Folder not found for {row.MonitorId}.");
            return false;
        }

        var imagePaths = Directory
            .EnumerateFiles(row.FolderPath)
            .Where(IsImageFile)
            .ToArray();

        if (imagePaths.Length == 0)
        {
            row.RestoreNextRun(DateTimeOffset.MaxValue);
            SetStatusMessage($"No images found in {row.FolderPath}.");
            return false;
        }

        var chosenImage = row.PeekNextImage(imagePaths);
        await snapshotApplyGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = Monitors
                .Where(monitor => !string.IsNullOrWhiteSpace(monitor.CurrentImagePath))
                .ToDictionary(monitor => monitor.MonitorId, monitor => monitor.CurrentImagePath!, StringComparer.OrdinalIgnoreCase);
            snapshot[row.MonitorId] = chosenImage;

            await wallpaperService.ApplyAsync(snapshot, cancellationToken);
            row.CurrentImagePath = chosenImage;
            row.ConsumeNextImage(imagePaths);
        }
        finally
        {
            snapshotApplyGate.Release();
        }

        SetStatusMessage($"Applied wallpaper for {row.MonitorId}.");
        return true;
    }

    private void SetStatusMessage(string message)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            StatusMessage = message;
            return;
        }

        dispatcher.Invoke(() => StatusMessage = message);
    }

    private static bool IsImageFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class MonitorRowViewModel : ObservableObject
{
    private readonly MainViewModel owner;
    private readonly IImagePicker imagePicker;
    private readonly SemaphoreSlim applyGate = new(1, 1);
    private string? folderPath;
    private string? currentImagePath;
    private int intervalValue;
    private string intervalUnit;

    public MonitorRowViewModel(MainViewModel owner, WallpaperMonitorProfile profile, IImagePicker imagePicker)
    {
        this.owner = owner;
        this.imagePicker = imagePicker;
        MonitorId = profile.MonitorId;
        folderPath = profile.FolderPath;
        currentImagePath = profile.LastAppliedImage;
        intervalValue = profile.IntervalValue;
        intervalUnit = profile.IntervalUnit;

        BrowseFolderCommand = new RelayCommand(() => owner.BrowseFolder(this));
        ApplyNowCommand = new AsyncRelayCommand(ApplyNowAsync, owner.ReportError);
    }

    public string MonitorId { get; }

    public string? FolderPath
    {
        get => folderPath;
        set
        {
            if (SetProperty(ref folderPath, value))
            {
                owner.Reschedule(this);
            }
        }
    }

    public string? CurrentImagePath
    {
        get => currentImagePath;
        internal set => SetProperty(ref currentImagePath, value);
    }

    public int IntervalValue
    {
        get => intervalValue;
        set
        {
            if (value < 1)
            {
                value = 1;
            }

            if (SetProperty(ref intervalValue, value))
            {
                owner.Reschedule(this);
            }
        }
    }

    public string IntervalUnit
    {
        get => intervalUnit;
        set
        {
            if (SetProperty(ref intervalUnit, value))
            {
                owner.Reschedule(this);
            }
        }
    }

    public IReadOnlyList<string> IntervalUnits { get; } = new[] { "minutes", "hours", "days" };

    public ICommand BrowseFolderCommand { get; }

    public ICommand ApplyNowCommand { get; }

    public DateTimeOffset NextRunAt
    {
        get => nextRunAt;
        private set => SetProperty(ref nextRunAt, value);
    }

    private DateTimeOffset nextRunAt;

    public Task BrowseFolderAsync()
    {
        owner.BrowseFolder(this);
        return Task.CompletedTask;
    }

    public Task ApplyNowAsync()
    {
        return ApplyNowAndRescheduleAsync();
    }

    internal Task ApplyIfDueAsync()
    {
        return ApplyNowAndRescheduleAsync(onlyIfDue: true);
    }

    public void ScheduleNextRun()
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            UpdateNextRunAt(DateTimeOffset.MaxValue);
            return;
        }

        if (IntervalValue < 1)
        {
            UpdateNextRunAt(DateTimeOffset.MaxValue);
            return;
        }

        try
        {
            UpdateNextRunAt(WallpaperScheduler.GetNextRun(DateTimeOffset.UtcNow, ToProfile()));
        }
        catch (ArgumentException)
        {
            UpdateNextRunAt(DateTimeOffset.MaxValue);
        }
    }

    private async Task ApplyNowAndRescheduleAsync(bool onlyIfDue = false)
    {
        await applyGate.WaitAsync();

        try
        {
            if (onlyIfDue && NextRunAt > DateTimeOffset.UtcNow)
            {
                return;
            }

            var applied = await owner.ApplyNowAsync(this);
            if (applied)
            {
                ScheduleNextRun();
            }

            await owner.PersistAsync();
        }
        finally
        {
            applyGate.Release();
        }
    }

    internal string PeekNextImage(IReadOnlyCollection<string> imagePaths)
    {
        return imagePicker.PeekNext(imagePaths);
    }

    internal void ConsumeNextImage(IReadOnlyCollection<string> imagePaths)
    {
        imagePicker.PickNext(imagePaths);
    }

    public WallpaperMonitorProfile ToProfile()
    {
        return new WallpaperMonitorProfile(MonitorId)
        {
            FolderPath = FolderPath,
            IntervalValue = IntervalValue,
            IntervalUnit = IntervalUnit,
            LastAppliedImage = CurrentImagePath,
            RemainingImages = imagePicker.RemainingImages,
            NextRunAt = NextRunAt
        };
    }

    internal void RestoreNextRun(DateTimeOffset nextRunAt)
    {
        UpdateNextRunAt(nextRunAt);
    }

    private void UpdateNextRunAt(DateTimeOffset nextRunAt)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            NextRunAt = nextRunAt;
            return;
        }

        dispatcher.Invoke(() => NextRunAt = nextRunAt);
    }
}

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action execute;

    public RelayCommand(Action execute)
    {
        this.execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> execute;
    private readonly Action<Exception>? onError;

    public AsyncRelayCommand(Func<Task> execute, Action<Exception>? onError = null)
    {
        this.execute = execute;
        this.onError = onError;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        try
        {
            await execute();
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }
    }
}
