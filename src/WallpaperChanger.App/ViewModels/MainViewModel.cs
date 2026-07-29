using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Models;

namespace WallpaperChanger.App.ViewModels;

public interface IFolderPicker
{
    string? PickFolder(string? initialFolder);
}

public sealed class MainViewModel
{
    private readonly ISettingsStore settingsStore;
    private readonly IMonitorRegistry monitorRegistry;
    private readonly IWallpaperService wallpaperService;
    private readonly Func<IImagePicker> imagePickerFactory;
    private readonly IFolderPicker folderPicker;
    private readonly Dictionary<string, WallpaperMonitorProfile> savedProfilesById = new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel(
        ISettingsStore settingsStore,
        IMonitorRegistry monitorRegistry,
        IWallpaperService wallpaperService,
        Func<IImagePicker> imagePickerFactory,
        IFolderPicker folderPicker)
    {
        this.settingsStore = settingsStore;
        this.monitorRegistry = monitorRegistry;
        this.wallpaperService = wallpaperService;
        this.imagePickerFactory = imagePickerFactory;
        this.folderPicker = folderPicker;
    }

    public ObservableCollection<MonitorRowViewModel> Monitors { get; } = new();

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
            Monitors.Add(new MonitorRowViewModel(this, profile ?? new WallpaperMonitorProfile(monitorId), imagePickerFactory()));
        }
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

    internal async Task ApplyNowAsync(MonitorRowViewModel row, CancellationToken cancellationToken = default)
    {
        await SaveAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(row.FolderPath) || !Directory.Exists(row.FolderPath))
        {
            return;
        }

        var imagePaths = Directory
            .EnumerateFiles(row.FolderPath)
            .Where(IsImageFile)
            .ToArray();

        if (imagePaths.Length == 0)
        {
            return;
        }

        var chosenImage = row.PickNextImage(imagePaths);
        await wallpaperService.SetWallpaperForMonitorAsync(row.MonitorId, chosenImage, cancellationToken);
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
        ApplyNowCommand = new AsyncRelayCommand(() => owner.ApplyNowAsync(this));
    }

    public string MonitorId { get; }

    public string? FolderPath
    {
        get => folderPath;
        set => SetProperty(ref folderPath, value);
    }

    public int IntervalValue
    {
        get => intervalValue;
        set => SetProperty(ref intervalValue, value);
    }

    public string IntervalUnit
    {
        get => intervalUnit;
        set => SetProperty(ref intervalUnit, value);
    }

    public IReadOnlyList<string> IntervalUnits { get; } = new[] { "minutes", "hours", "days" };

    public ICommand BrowseFolderCommand { get; }

    public ICommand ApplyNowCommand { get; }

    public Task BrowseFolderAsync()
    {
        owner.BrowseFolder(this);
        return Task.CompletedTask;
    }

    public Task ApplyNowAsync()
    {
        return owner.ApplyNowAsync(this);
    }

    internal string PickNextImage(IReadOnlyCollection<string> imagePaths)
    {
        return imagePicker.PickNext(imagePaths);
    }

    public WallpaperMonitorProfile ToProfile()
    {
        return new WallpaperMonitorProfile(MonitorId)
        {
            FolderPath = FolderPath,
            IntervalValue = IntervalValue,
            IntervalUnit = IntervalUnit
        };
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

    public AsyncRelayCommand(Func<Task> execute)
    {
        this.execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        await execute();
    }
}
