using WallpaperChanger.App.ViewModels;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Models;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class MainViewModelTests
{
    [Fact]
    public async Task InitializeAsync_creates_a_row_for_each_connected_monitor_and_loads_saved_values()
    {
        var settings = new FakeSettingsStore(
            new[]
            {
                new WallpaperMonitorProfile("monitor-2")
                {
                    FolderPath = "C:/Wallpapers/Monitor2",
                    IntervalValue = 15,
                    IntervalUnit = "hours"
                }
            });
        var registry = new FakeMonitorRegistry("monitor-1", "monitor-2");
        var wallpaper = new FakeWallpaperService();
        var imagePicker = new FakeImagePicker();
        var folderPicker = new FakeFolderPicker();

        var vm = new MainViewModel(settings, registry, wallpaper, _ => imagePicker, folderPicker);

        await vm.InitializeAsync();

        Assert.Equal(2, vm.Monitors.Count);
        Assert.Equal("monitor-1", vm.Monitors[0].MonitorId);
        Assert.Null(vm.Monitors[0].FolderPath);
        Assert.Equal(1, vm.Monitors[0].IntervalValue);
        Assert.Equal("minutes", vm.Monitors[0].IntervalUnit);
        Assert.Equal("monitor-2", vm.Monitors[1].MonitorId);
        Assert.Equal("C:/Wallpapers/Monitor2", vm.Monitors[1].FolderPath);
        Assert.Equal(15, vm.Monitors[1].IntervalValue);
        Assert.Equal("hours", vm.Monitors[1].IntervalUnit);
    }

    [Fact]
    public async Task BrowseFolderAsync_updates_the_selected_folder_path()
    {
        var settings = new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>());
        var registry = new FakeMonitorRegistry("monitor-1");
        var wallpaper = new FakeWallpaperService();
        var imagePicker = new FakeImagePicker();
        var folderPicker = new FakeFolderPicker { FolderToReturn = "C:/Wallpapers/Monitor1" };

        var vm = new MainViewModel(settings, registry, wallpaper, _ => imagePicker, folderPicker);

        await vm.InitializeAsync();

        await vm.Monitors[0].BrowseFolderAsync();

        Assert.Equal("C:/Wallpapers/Monitor1", vm.Monitors[0].FolderPath);
        Assert.Equal("C:/Wallpapers/Monitor1", folderPicker.LastInitialFolder);
    }

    [Fact]
    public async Task ApplyNowAsync_saves_rows_and_applies_the_picked_image()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        var imagePath = Path.Combine(folder, "chosen.jpg");
        await File.WriteAllTextAsync(imagePath, string.Empty);

        try
        {
            var settings = new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>());
            var registry = new FakeMonitorRegistry("monitor-1");
            var wallpaper = new FakeWallpaperService();
            var imagePicker = new FakeImagePicker { ImageToReturn = imagePath };
            var folderPicker = new FakeFolderPicker();

            var vm = new MainViewModel(settings, registry, wallpaper, _ => imagePicker, folderPicker);

            await vm.InitializeAsync();
            vm.Monitors[0].FolderPath = folder;
            vm.Monitors[0].IntervalValue = 20;
            vm.Monitors[0].IntervalUnit = "minutes";

            await vm.Monitors[0].ApplyNowAsync();

            Assert.NotNull(settings.SavedProfiles);
            Assert.Single(settings.SavedProfiles!);
            var saved = settings.SavedProfiles!.Single();
            Assert.Equal(folder, saved.FolderPath);
            Assert.Equal(20, saved.IntervalValue);
            Assert.Equal("minutes", saved.IntervalUnit);
            Assert.Equal(("monitor-1", imagePath), wallpaper.LastCall);
            Assert.NotNull(imagePicker.LastImagePaths);
            Assert.Contains(imagePath, imagePicker.LastImagePaths!);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task InitializeAsync_creates_an_image_picker_per_monitor()
    {
        var settings = new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>());
        var registry = new FakeMonitorRegistry("monitor-1", "monitor-2");
        var wallpaper = new FakeWallpaperService();
        var createdPickers = 0;
        var folderPicker = new FakeFolderPicker();

        var vm = new MainViewModel(
            settings,
            registry,
            wallpaper,
            _ =>
            {
                createdPickers++;
                return new FakeImagePicker();
            },
            folderPicker);

        await vm.InitializeAsync();

        Assert.Equal(2, createdPickers);
    }

    [Fact]
    public async Task ApplyNowAsync_preserves_profiles_for_disconnected_monitors()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        var imagePath = Path.Combine(folder, "chosen.jpg");
        await File.WriteAllTextAsync(imagePath, string.Empty);

        try
        {
            var settings = new FakeSettingsStore(
                new[]
                {
                    new WallpaperMonitorProfile("monitor-1")
                    {
                        FolderPath = "C:/Connected",
                        IntervalValue = 20,
                        IntervalUnit = "minutes"
                    },
                    new WallpaperMonitorProfile("monitor-2")
                    {
                        FolderPath = "C:/Disconnected",
                        IntervalValue = 45,
                        IntervalUnit = "hours"
                    }
                });
            var registry = new FakeMonitorRegistry("monitor-1");
            var wallpaper = new FakeWallpaperService();
            var imagePicker = new FakeImagePicker { ImageToReturn = imagePath };
            var folderPicker = new FakeFolderPicker();

            var vm = new MainViewModel(settings, registry, wallpaper, _ => imagePicker, folderPicker);

            await vm.InitializeAsync();
            vm.Monitors[0].FolderPath = folder;

            await vm.Monitors[0].ApplyNowAsync();

            Assert.NotNull(settings.SavedProfiles);
            Assert.Equal(2, settings.SavedProfiles!.Count);
            Assert.Contains(settings.SavedProfiles!, profile =>
                profile.MonitorId == "monitor-1" && profile.FolderPath == folder && profile.IntervalValue == 20);
            Assert.Contains(settings.SavedProfiles!, profile =>
                profile.MonitorId == "monitor-2" && profile.FolderPath == "C:/Disconnected" && profile.IntervalValue == 45);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PersistAsync_saves_in_memory_edits_without_apply_now()
    {
        var settings = new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>());
        var registry = new FakeMonitorRegistry("monitor-1");
        var wallpaper = new FakeWallpaperService();
        var imagePicker = new FakeImagePicker();
        var folderPicker = new FakeFolderPicker();

        var vm = new MainViewModel(settings, registry, wallpaper, _ => imagePicker, folderPicker);

        await vm.InitializeAsync();
        vm.Monitors[0].FolderPath = "C:/Wallpapers/Monitor1";
        vm.Monitors[0].IntervalValue = 30;
        vm.Monitors[0].IntervalUnit = "hours";

        await vm.PersistAsync();

        Assert.NotNull(settings.SavedProfiles);
        Assert.Single(settings.SavedProfiles!);
        var saved = settings.SavedProfiles!.Single();
        Assert.Equal("C:/Wallpapers/Monitor1", saved.FolderPath);
        Assert.Equal(30, saved.IntervalValue);
        Assert.Equal("hours", saved.IntervalUnit);
        Assert.Equal(vm.Monitors[0].NextRunAt, saved.NextRunAt);
    }

    [Fact]
    public void AsyncRelayCommand_reports_exceptions_from_the_delegate()
    {
        string? reportedMessage = null;
        var command = new AsyncRelayCommand(
            () => throw new InvalidOperationException("boom"),
            ex => reportedMessage = ex.Message);

        var exception = Record.Exception(() => command.Execute(null));

        Assert.Null(exception);
        Assert.Equal("boom", reportedMessage);
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly IReadOnlyList<WallpaperMonitorProfile> loadedProfiles;

        public FakeSettingsStore(IReadOnlyList<WallpaperMonitorProfile> loadedProfiles)
        {
            this.loadedProfiles = loadedProfiles;
        }

        public IReadOnlyCollection<WallpaperMonitorProfile>? SavedProfiles { get; private set; }

        public Task<IReadOnlyList<WallpaperMonitorProfile>> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(loadedProfiles);
        }

        public Task SaveAsync(IReadOnlyCollection<WallpaperMonitorProfile> profiles, CancellationToken cancellationToken = default)
        {
            SavedProfiles = profiles.ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMonitorRegistry : IMonitorRegistry
    {
        private readonly IReadOnlyList<string> monitorIds;

        public FakeMonitorRegistry(params string[] monitorIds)
        {
            this.monitorIds = monitorIds;
        }

        public IReadOnlyList<string> GetConnectedMonitorIds() => monitorIds;
    }

    private sealed class FakeWallpaperService : IWallpaperService
    {
        public (string MonitorId, string ImagePath)? LastCall { get; private set; }

        public Task SetWallpaperForMonitorAsync(string monitorId, string imagePath, CancellationToken cancellationToken = default)
        {
            LastCall = (monitorId, imagePath);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeImagePicker : IImagePicker
    {
        public IReadOnlyCollection<string>? LastImagePaths { get; private set; }

        public string? ImageToReturn { get; set; }

        private string? lastPickedImage;

        public string? LastPickedImage => lastPickedImage;

        public IReadOnlyList<string> RemainingImages => LastImagePaths?.ToArray() ?? Array.Empty<string>();

        public string PeekNext(IReadOnlyCollection<string> imagePaths)
        {
            return ImageToReturn ?? imagePaths.First();
        }

        public string PickNext(IReadOnlyCollection<string> imagePaths)
        {
            LastImagePaths = imagePaths.ToArray();
            lastPickedImage = PeekNext(imagePaths);
            return lastPickedImage;
        }
    }

    private sealed class FakeFolderPicker : IFolderPicker
    {
        public string? FolderToReturn { get; set; }

        public string? LastInitialFolder { get; private set; }

        public string? PickFolder(string? initialFolder)
        {
            LastInitialFolder = initialFolder;
            return FolderToReturn;
        }
    }
}
