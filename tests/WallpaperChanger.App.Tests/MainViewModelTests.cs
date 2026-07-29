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
                    IntervalUnit = "hours",
                    LastAppliedImage = "two.jpg"
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
        Assert.Equal("two.jpg", vm.Monitors[1].CurrentImagePath);
    }

    [Fact]
    public async Task InitializeAsync_restores_last_applied_image_for_snapshot_composition()
    {
        var settings = new FakeSettingsStore(
            new[]
            {
                new WallpaperMonitorProfile("monitor-1") { LastAppliedImage = "one.jpg" }
            });
        var vm = new MainViewModel(
            settings,
            new FakeMonitorRegistry("monitor-1"),
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());

        await vm.InitializeAsync();

        Assert.Equal("one.jpg", vm.Monitors[0].CurrentImagePath);
    }

    [Fact]
    public async Task RecomposeAsync_after_topology_refresh_retains_known_images_and_applies_one_snapshot()
    {
        var settings = new FakeSettingsStore(
            new[]
            {
                new WallpaperMonitorProfile("monitor-1") { LastAppliedImage = "one.jpg" },
                new WallpaperMonitorProfile("monitor-2") { LastAppliedImage = "two.jpg" }
            });
        var registry = new FakeMonitorRegistry("monitor-1", "monitor-2");
        var wallpaper = new FakeWallpaperService();
        var pickers = new List<FakeImagePicker>();
        var vm = new MainViewModel(
            settings,
            registry,
            wallpaper,
            _ =>
            {
                var picker = new FakeImagePicker();
                pickers.Add(picker);
                return picker;
            },
            new FakeFolderPicker());

        await vm.InitializeAsync();
        registry.SetConnectedMonitors("monitor-2", "monitor-1");
        await vm.InitializeAsync();
        await vm.RecomposeAsync();

        Assert.Equal("monitor-2", vm.Monitors[0].MonitorId);
        Assert.Equal("two.jpg", vm.Monitors[0].CurrentImagePath);
        Assert.Equal("one.jpg", vm.Monitors[1].CurrentImagePath);
        Assert.Equal(1, wallpaper.ApplyCount);
        Assert.Equal("one.jpg", wallpaper.LastSnapshot!["monitor-1"]);
        Assert.Equal("two.jpg", wallpaper.LastSnapshot["monitor-2"]);
        Assert.All(pickers, picker => Assert.Equal(0, picker.PickCount));
    }

    [Fact]
    public async Task InitializeAsync_disables_rows_with_missing_folders()
    {
        var settings = new FakeSettingsStore(
            new[]
            {
                new WallpaperMonitorProfile("monitor-1")
                {
                    FolderPath = "C:/Does/Not/Exist",
                    IntervalValue = 15,
                    IntervalUnit = "minutes"
                }
            });
        var registry = new FakeMonitorRegistry("monitor-1");
        var wallpaper = new FakeWallpaperService();
        var imagePicker = new FakeImagePicker();
        var folderPicker = new FakeFolderPicker();

        var vm = new MainViewModel(settings, registry, wallpaper, _ => imagePicker, folderPicker);

        await vm.InitializeAsync();

        Assert.Equal(DateTimeOffset.MaxValue, vm.Monitors[0].NextRunAt);
    }

    [Fact]
    public async Task InitializeAsync_skips_saved_profiles_with_missing_monitor_ids()
    {
        var settings = new FakeSettingsStore(
            new[]
            {
                new WallpaperMonitorProfile("")
                {
                    FolderPath = "C:/Invalid",
                    IntervalValue = 15,
                    IntervalUnit = "minutes"
                },
                new WallpaperMonitorProfile("monitor-1")
                {
                    FolderPath = Path.GetTempPath(),
                    IntervalValue = 15,
                    IntervalUnit = "minutes"
                }
            });
        var registry = new FakeMonitorRegistry("monitor-1");
        var wallpaper = new FakeWallpaperService();
        var imagePicker = new FakeImagePicker();
        var folderPicker = new FakeFolderPicker();

        var vm = new MainViewModel(settings, registry, wallpaper, _ => imagePicker, folderPicker);

        await vm.InitializeAsync();

        Assert.Single(vm.Monitors);
        Assert.Equal("monitor-1", vm.Monitors[0].MonitorId);
    }

    [Fact]
    public async Task InitializeAsync_clamps_overflowing_intervals()
    {
        var settings = new FakeSettingsStore(
            new[]
            {
                new WallpaperMonitorProfile("monitor-1")
                {
                    FolderPath = Path.GetTempPath(),
                    IntervalValue = int.MaxValue,
                    IntervalUnit = "days"
                }
            });
        var registry = new FakeMonitorRegistry("monitor-1");
        var wallpaper = new FakeWallpaperService();
        var imagePicker = new FakeImagePicker();
        var folderPicker = new FakeFolderPicker();

        var vm = new MainViewModel(settings, registry, wallpaper, _ => imagePicker, folderPicker);

        await vm.InitializeAsync();

        Assert.Equal(DateTimeOffset.MaxValue, vm.Monitors[0].NextRunAt);
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
        Assert.Null(folderPicker.LastInitialFolder);
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
            Assert.Equal(imagePath, wallpaper.LastSnapshot!["monitor-1"]);
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
    public async Task ApplyNowAsync_preserves_other_monitor_images_in_snapshot()
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
                    new WallpaperMonitorProfile("monitor-2") { LastAppliedImage = "two.jpg" }
                });
            var wallpaper = new FakeWallpaperService();
            var vm = new MainViewModel(
                settings,
                new FakeMonitorRegistry("monitor-1", "monitor-2"),
                wallpaper,
                _ => new FakeImagePicker { ImageToReturn = imagePath },
                new FakeFolderPicker());

            await vm.InitializeAsync();
            vm.Monitors[0].FolderPath = folder;

            await vm.Monitors[0].ApplyNowAsync();

            Assert.Equal(imagePath, wallpaper.LastSnapshot!["monitor-1"]);
            Assert.Equal("two.jpg", wallpaper.LastSnapshot["monitor-2"]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyNowAsync_keeps_current_image_when_snapshot_apply_fails()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var imagePath = Path.Combine(folder, "chosen.jpg");
        await File.WriteAllTextAsync(imagePath, string.Empty);

        try
        {
            var vm = new MainViewModel(
                new FakeSettingsStore(new[] { new WallpaperMonitorProfile("monitor-1") { LastAppliedImage = "old.jpg" } }),
                new FakeMonitorRegistry("monitor-1"),
                new ThrowingWallpaperService(),
                _ => new FakeImagePicker { ImageToReturn = imagePath },
                new FakeFolderPicker());

            await vm.InitializeAsync();
            vm.Monitors[0].FolderPath = folder;

            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.Monitors[0].ApplyNowAsync());

            Assert.Equal("old.jpg", vm.Monitors[0].CurrentImagePath);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyNowAsync_serializes_snapshots_across_monitors()
    {
        var firstFolder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        var secondFolder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(firstFolder);
        Directory.CreateDirectory(secondFolder);
        var firstImage = Path.Combine(firstFolder, "one.jpg");
        var secondImage = Path.Combine(secondFolder, "two.jpg");
        await File.WriteAllTextAsync(firstImage, string.Empty);
        await File.WriteAllTextAsync(secondImage, string.Empty);

        try
        {
            var wallpaper = new CoordinatedWallpaperService();
            var vm = new MainViewModel(
                new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
                new FakeMonitorRegistry("monitor-1", "monitor-2"),
                wallpaper,
                profile => new FakeImagePicker
                {
                    ImageToReturn = profile.MonitorId == "monitor-1" ? firstImage : secondImage
                },
                new FakeFolderPicker());

            await vm.InitializeAsync();
            vm.Monitors[0].FolderPath = firstFolder;
            vm.Monitors[1].FolderPath = secondFolder;

            var firstApply = vm.Monitors[0].ApplyNowAsync();
            await wallpaper.FirstApplyStarted.Task;
            var secondApply = vm.Monitors[1].ApplyNowAsync();
            wallpaper.ReleaseFirstApply();
            await wallpaper.SecondApplyStarted.Task;
            wallpaper.ReleaseSecondApply();
            await Task.WhenAll(firstApply, secondApply);

            Assert.Equal(firstImage, wallpaper.LastSnapshot!["monitor-1"]);
            Assert.Equal(secondImage, wallpaper.LastSnapshot["monitor-2"]);
        }
        finally
        {
            Directory.Delete(firstFolder, recursive: true);
            Directory.Delete(secondFolder, recursive: true);
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
        private IReadOnlyList<MonitorDescriptor> monitors;

        public FakeMonitorRegistry(params string[] monitorIds)
        {
            monitors = monitorIds
                .Select((monitorId, index) => new MonitorDescriptor(monitorId, monitorId, index, 0, 1, 1, index == 0))
                .ToArray();
        }

        public void SetConnectedMonitors(params string[] monitorIds)
        {
            monitors = monitorIds
                .Select((monitorId, index) => new MonitorDescriptor(monitorId, monitorId, index, 0, 1, 1, index == 0))
                .ToArray();
        }

        public IReadOnlyList<MonitorDescriptor> GetConnectedMonitors() => monitors;
    }

    private sealed class FakeWallpaperService : IWallpaperService
    {
        public IReadOnlyDictionary<string, string>? LastSnapshot { get; private set; }

        public int ApplyCount { get; private set; }

        public Task ApplyAsync(IReadOnlyDictionary<string, string> imagePathsByMonitorId, CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            LastSnapshot = new Dictionary<string, string>(imagePathsByMonitorId);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingWallpaperService : IWallpaperService
    {
        public Task ApplyAsync(IReadOnlyDictionary<string, string> imagePathsByMonitorId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("apply failed");
        }
    }

    private sealed class CoordinatedWallpaperService : IWallpaperService
    {
        private readonly TaskCompletionSource firstApplyStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource secondApplyStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseFirstApply = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseSecondApply = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int applyCount;

        public TaskCompletionSource FirstApplyStarted => firstApplyStarted;

        public TaskCompletionSource SecondApplyStarted => secondApplyStarted;

        public IReadOnlyDictionary<string, string>? LastSnapshot { get; private set; }

        public void ReleaseFirstApply() => releaseFirstApply.SetResult();

        public void ReleaseSecondApply() => releaseSecondApply.SetResult();

        public async Task ApplyAsync(IReadOnlyDictionary<string, string> imagePathsByMonitorId, CancellationToken cancellationToken = default)
        {
            LastSnapshot = new Dictionary<string, string>(imagePathsByMonitorId);

            if (Interlocked.Increment(ref applyCount) == 1)
            {
                firstApplyStarted.SetResult();
                await releaseFirstApply.Task;
                return;
            }

            secondApplyStarted.SetResult();
            await releaseSecondApply.Task;
        }
    }

    private sealed class FakeImagePicker : IImagePicker
    {
        public IReadOnlyCollection<string>? LastImagePaths { get; private set; }

        public string? ImageToReturn { get; set; }

        private string? lastPickedImage;

        public int PickCount { get; private set; }

        public string? LastPickedImage => lastPickedImage;

        public IReadOnlyList<string> RemainingImages => LastImagePaths?.ToArray() ?? Array.Empty<string>();

        public string PeekNext(IReadOnlyCollection<string> imagePaths)
        {
            return ImageToReturn ?? imagePaths.First();
        }

        public string PickNext(IReadOnlyCollection<string> imagePaths)
        {
            PickCount++;
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
