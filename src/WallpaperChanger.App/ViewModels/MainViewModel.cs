using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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
    private string? statusMessage;

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

    public string? StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var savedProfiles = await settingsStore.LoadAsync(cancellationToken);
        savedProfilesById.Clear();

        foreach (var profile in savedProfiles)
        {
            savedProfilesById[profile.MonitorId] = profile;
        }

        Monitors.Clear();

        foreach (var monitorId in monitorRegistry.GetConnectedMonitorIds())
        {
            savedProfilesById.TryGetValue(monitorId, out var profile);
            var rowProfile = profile ?? new WallpaperMonitorProfile(monitorId);
            var row = new MonitorRowViewModel(this, rowProfile, imagePickerFactory(rowProfile));
            if (rowProfile.NextRunAt is { } nextRunAt)
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

    public Task PersistAsync(CancellationToken cancellationToken = default)
    {
        return SaveAsync(cancellationToken);
    }

    private Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var profilesById = new Dictionary<string, WallpaperMonitorProfile>(savedProfilesById, StringComparer.OrdinalIgnoreCase);

        foreach (var row in Monitors)
        {
            profilesById[row.MonitorId] = row.ToProfile();
        }

        savedProfilesById.Clear();
        foreach (var pair in profilesById)
        {
            savedProfilesById[pair.Key] = pair.Value;
        }

        return settingsStore.SaveAsync(profilesById.Values.ToArray(), cancellationToken);
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
        StatusMessage = exception.Message;
    }

    internal async Task ApplyNowAsync(MonitorRowViewModel row, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(row.FolderPath) || !Directory.Exists(row.FolderPath))
        {
            await SaveAsync(cancellationToken);
            StatusMessage = $"Folder not found for {row.MonitorId}.";
            return;
        }

        var imagePaths = Directory
            .EnumerateFiles(row.FolderPath)
            .Where(IsImageFile)
            .ToArray();

        if (imagePaths.Length == 0)
        {
            await SaveAsync(cancellationToken);
            StatusMessage = $"No images found in {row.FolderPath}.";
            return;
        }

        var chosenImage = row.PeekNextImage(imagePaths);
        await wallpaperService.SetWallpaperForMonitorAsync(row.MonitorId, chosenImage, cancellationToken);
        row.ConsumeNextImage(imagePaths);
        await SaveAsync(cancellationToken);
        StatusMessage = $"Applied wallpaper for {row.MonitorId}.";
    }

    private static bool IsImageFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class MonitorRowViewModel : ObservableObject
{
    private readonly MainViewModel owner;
    private readonly IImagePicker imagePicker;
    private string? folderPath;
    private int intervalValue;
    private string intervalUnit;

    public MonitorRowViewModel(MainViewModel owner, WallpaperMonitorProfile profile, IImagePicker imagePicker)
    {
        this.owner = owner;
        this.imagePicker = imagePicker;
        MonitorId = profile.MonitorId;
        folderPath = profile.FolderPath;
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

    public void ScheduleNextRun()
    {
        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            NextRunAt = DateTimeOffset.MaxValue;
            return;
        }

        if (IntervalValue < 1)
        {
            NextRunAt = DateTimeOffset.MaxValue;
            return;
        }

        NextRunAt = WallpaperScheduler.GetNextRun(DateTimeOffset.UtcNow, ToProfile());
    }

    private async Task ApplyNowAndRescheduleAsync()
    {
        await owner.ApplyNowAsync(this);
        ScheduleNextRun();
        await owner.PersistAsync();
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
            LastAppliedImage = imagePicker.LastPickedImage,
            RemainingImages = imagePicker.RemainingImages,
            NextRunAt = NextRunAt
        };
    }

    internal void RestoreNextRun(DateTimeOffset nextRunAt)
    {
        NextRunAt = nextRunAt;
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
