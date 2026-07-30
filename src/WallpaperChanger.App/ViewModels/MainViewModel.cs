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
        NewProposalCommand = new RelayCommand(NewProposalForSelectedMonitor);
    }

    public ObservableCollection<MonitorRowViewModel> Monitors { get; } = new();

    public ObservableCollection<VirtualMonitorViewModel> VirtualMonitors { get; } = new();

    public VirtualMonitorViewModel? SelectedVirtualMonitor
    {
        get => selectedVirtualMonitor;
        set
        {
            if (value is null || !VirtualMonitors.Contains(value))
            {
                value = VirtualMonitors.Count > 0 ? VirtualMonitors[0] : null;
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

    public ICommand NewProposalCommand { get; }

    private void UpdateVirtualMonitors(IReadOnlyList<MonitorDescriptor> monitors, string? selectedMonitorId)
    {
        SetProperty(ref selectedVirtualMonitor, null, nameof(SelectedVirtualMonitor));
        VirtualMonitors.Clear();

        if (monitors.Count == 0)
        {
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
            savedProfilesById.TryGetValue(monitor.Id, out var profile);
            VirtualMonitors.Add(new VirtualMonitorViewModel(
                monitor,
                (double)(monitor.Left - left) / width,
                (double)(monitor.Top - top) / height,
                (double)monitor.Width / width,
                (double)monitor.Height / height,
                profile?.LastAppliedImage,
                (double)width / height));
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
            NewProposal(row);
        }
    }

    private void NewProposalForSelectedMonitor()
    {
        if (SelectedVirtualMonitor is null)
        {
            SetStatusMessage("No monitor is available; connect a monitor and try again.");
            return;
        }

        var row = Monitors.FirstOrDefault(monitor =>
            string.Equals(monitor.MonitorId, SelectedVirtualMonitor.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
        {
            NewProposal(row);
        }
    }

    internal void NewProposal(MonitorRowViewModel row)
    {
        if (string.IsNullOrWhiteSpace(row.FolderPath) || !Directory.Exists(row.FolderPath))
        {
            row.SetProposal(null, 0, $"Folder not found for {row.MonitorId}.");
            SetVirtualMonitorProposal(row, null);
            SetStatusMessage(row.ProposalStatus);
            return;
        }

        string[] imagePaths;
        try
        {
            imagePaths = Directory.EnumerateFiles(row.FolderPath).Where(IsImageFile).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            row.SetProposal(null, 0, $"Folder not found for {row.MonitorId}.");
            SetVirtualMonitorProposal(row, null);
            SetStatusMessage(row.ProposalStatus);
            return;
        }

        if (imagePaths.Length == 0)
        {
            row.SetProposal(null, 0, $"No images found in {row.FolderPath}.");
            SetVirtualMonitorProposal(row, null);
            SetStatusMessage(row.ProposalStatus);
            return;
        }

        var proposedImage = imagePaths[Random.Shared.Next(imagePaths.Length)];
        row.SetProposal(proposedImage, imagePaths.Length, $"Proposed {Path.GetFileName(proposedImage)} for {row.MonitorId}.");
        SetVirtualMonitorProposal(row, proposedImage);
        SetStatusMessage(row.ProposalStatus);
    }

    private void SetVirtualMonitorProposal(MonitorRowViewModel row, string? proposedImage)
    {
        var virtualMonitor = VirtualMonitors.FirstOrDefault(monitor =>
            string.Equals(monitor.MonitorId, row.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (virtualMonitor is not null)
        {
            virtualMonitor.ProposedImagePath = proposedImage;
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
            var virtualMonitor = VirtualMonitors.FirstOrDefault(monitor =>
                string.Equals(monitor.MonitorId, row.MonitorId, StringComparison.OrdinalIgnoreCase));
            if (virtualMonitor is not null)
            {
                virtualMonitor.CurrentImagePath = chosenImage;
            }
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
    private string? proposedImagePath;
    private string? proposedImageFileName;
    private string proposalStatus = string.Empty;
    private int imageCount;
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
        NewProposalCommand = new RelayCommand(() => owner.NewProposal(this));
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

    public string? ProposedImagePath
    {
        get => proposedImagePath;
        private set => SetProperty(ref proposedImagePath, value);
    }

    public string? ProposedImageFileName
    {
        get => proposedImageFileName;
        private set => SetProperty(ref proposedImageFileName, value);
    }

    public string ProposalStatus
    {
        get => proposalStatus;
        private set => SetProperty(ref proposalStatus, value);
    }

    public int ImageCount
    {
        get => imageCount;
        private set => SetProperty(ref imageCount, value);
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

    public ICommand NewProposalCommand { get; }

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

    internal void SetProposal(string? imagePath, int count, string status)
    {
        ProposedImagePath = imagePath;
        ProposedImageFileName = imagePath is null ? null : Path.GetFileName(imagePath);
        ImageCount = count;
        ProposalStatus = status;
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
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
