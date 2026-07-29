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
    private readonly IImagePicker imagePicker;
    private readonly IFolderPicker folderPicker;

    public MainViewModel(
        ISettingsStore settingsStore,
        IMonitorRegistry monitorRegistry,
        IWallpaperService wallpaperService,
        IImagePicker imagePicker,
        IFolderPicker folderPicker)
    {
        this.settingsStore = settingsStore;
        this.monitorRegistry = monitorRegistry;
        this.wallpaperService = wallpaperService;
        this.imagePicker = imagePicker;
        this.folderPicker = folderPicker;
    }

    public ObservableCollection<MonitorRowViewModel> Monitors { get; } = new();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var savedProfiles = await settingsStore.LoadAsync(cancellationToken);
        var profilesById = savedProfiles
            .GroupBy(profile => profile.MonitorId)
            .ToDictionary(group => group.Key, group => group.First());

        Monitors.Clear();

        foreach (var monitorId in monitorRegistry.GetConnectedMonitorIds())
        {
            profilesById.TryGetValue(monitorId, out var profile);
            Monitors.Add(new MonitorRowViewModel(this, profile ?? new WallpaperMonitorProfile(monitorId)));
        }
    }

    private Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var profiles = Monitors.Select(row => row.ToProfile()).ToArray();
        return settingsStore.SaveAsync(profiles, cancellationToken);
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

        var chosenImage = imagePicker.PickNext(imagePaths);
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
    private string? folderPath;
    private int intervalValue;
    private string intervalUnit;

    public MonitorRowViewModel(MainViewModel owner, WallpaperMonitorProfile profile)
    {
        this.owner = owner;
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
