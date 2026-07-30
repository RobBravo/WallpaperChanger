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
    public async Task InitializeAsync_creates_virtual_monitors_with_normalized_bounds_and_orientation()
    {
        var registry = new FakeMonitorRegistry(
            new MonitorDescriptor("portrait", "DISPLAY2", -1080, 0, 1080, 1920, false),
            new MonitorDescriptor("primary", "DISPLAY1", 0, 0, 1920, 1080, true));
        var vm = new MainViewModel(
            new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
            registry,
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());

        await vm.InitializeAsync();

        Assert.Collection(
            vm.VirtualMonitors,
            portrait =>
            {
                Assert.Equal("portrait", portrait.MonitorId);
                Assert.Equal(0, portrait.NormalizedLeft);
                Assert.Equal(0, portrait.NormalizedTop);
                Assert.Equal(1080d / 3000d, portrait.NormalizedWidth, 6);
                Assert.Equal(1, portrait.NormalizedHeight);
                Assert.Equal(3000d / 1920d, portrait.LayoutAspectRatio, 6);
                Assert.True(portrait.IsPortrait);
            },
            primary =>
            {
                Assert.Equal("primary", primary.MonitorId);
                Assert.Equal(1080d / 3000d, primary.NormalizedLeft, 6);
                Assert.Equal(0, primary.NormalizedTop);
                Assert.Equal(1920d / 3000d, primary.NormalizedWidth, 6);
                Assert.Equal(1080d / 1920d, primary.NormalizedHeight, 6);
                Assert.False(primary.IsPortrait);
            });
        Assert.Equal("portrait", vm.SelectedVirtualMonitor!.MonitorId);
    }

    [Fact]
    public async Task InitializeAsync_exposes_the_saved_image_for_each_virtual_monitor_preview()
    {
        var vm = new MainViewModel(
            new FakeSettingsStore(new[]
            {
                new WallpaperMonitorProfile("monitor-1") { LastAppliedImage = "preview.jpg" }
            }),
            new FakeMonitorRegistry("monitor-1"),
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());

        await vm.InitializeAsync();

        Assert.Equal("preview.jpg", vm.VirtualMonitors.Single().CurrentImagePath);
    }

    [Fact]
    public async Task RefreshAsync_keeps_the_selected_virtual_monitor_when_its_id_is_still_connected()
    {
        var registry = new FakeMonitorRegistry(
            new MonitorDescriptor("right", "DISPLAY3", 1920, 0, 1920, 1080, false),
            new MonitorDescriptor("primary", "DISPLAY1", 0, 0, 1920, 1080, true));
        var vm = new MainViewModel(
            new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
            registry,
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());

        await vm.InitializeAsync();
        vm.SelectedVirtualMonitor = vm.VirtualMonitors.Single(monitor => monitor.MonitorId == "right");

        registry.SetConnectedMonitors(
            new MonitorDescriptor("right", "DISPLAY3", 0, 0, 1080, 1920, false),
            new MonitorDescriptor("primary", "DISPLAY1", 1080, 0, 1920, 1080, true));
        await vm.RefreshAsync();

        Assert.Equal(new[] { "right", "primary" }, vm.VirtualMonitors.Select(monitor => monitor.MonitorId));
        Assert.Equal("right", vm.SelectedVirtualMonitor!.MonitorId);
        Assert.True(vm.SelectedVirtualMonitor.IsPortrait);
    }

    [Fact]
    public async Task SelectedVirtualMonitor_replaces_an_external_monitor_with_the_first_current_monitor()
    {
        var vm = new MainViewModel(
            new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
            new FakeMonitorRegistry(
                new MonitorDescriptor("left", "DISPLAY1", 0, 0, 1920, 1080, true),
                new MonitorDescriptor("right", "DISPLAY2", 1920, 0, 1920, 1080, false)),
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());

        await vm.InitializeAsync();
        vm.SelectedVirtualMonitor = new VirtualMonitorViewModel(
            new MonitorDescriptor("stale", "DISPLAY3", 0, 0, 1, 1, false),
            0,
            0,
            1,
            1);

        Assert.Same(vm.VirtualMonitors[0], vm.SelectedVirtualMonitor);
    }

    [Fact]
    public async Task RefreshAsync_never_exposes_a_stale_selection_during_virtual_monitor_rebuild()
    {
        var registry = new FakeMonitorRegistry("left", "right");
        var vm = new MainViewModel(
            new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
            registry,
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());

        await vm.InitializeAsync();
        vm.VirtualMonitors.CollectionChanged += (_, _) =>
            Assert.True(vm.SelectedVirtualMonitor is null || vm.VirtualMonitors.Contains(vm.SelectedVirtualMonitor));

        registry.SetConnectedMonitors("right", "left");
        await vm.RefreshAsync();
    }

    [Fact]
    public async Task RefreshAsync_clears_selection_before_virtual_monitors_become_empty()
    {
        var registry = new FakeMonitorRegistry("monitor-1");
        var vm = new MainViewModel(
            new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
            registry,
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());

        await vm.InitializeAsync();
        vm.VirtualMonitors.CollectionChanged += (_, _) =>
            Assert.True(vm.SelectedVirtualMonitor is null || vm.VirtualMonitors.Contains(vm.SelectedVirtualMonitor));

        registry.SetConnectedMonitors(Array.Empty<string>());
        await vm.RefreshAsync();

        Assert.Null(vm.SelectedVirtualMonitor);
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
    public async Task RefreshAsync_waits_for_an_in_progress_apply_to_consume_its_image_before_replacing_rows()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var imagePath = Path.Combine(folder, "chosen.jpg");
        await File.WriteAllTextAsync(imagePath, string.Empty);

        try
        {
            var registry = new FakeMonitorRegistry("monitor-1");
            var wallpaper = new CoordinatedWallpaperService();
            var picker = new FakeImagePicker { ImageToReturn = imagePath };
            var vm = new MainViewModel(
                new FakeSettingsStore(new[] { new WallpaperMonitorProfile("monitor-1") { LastAppliedImage = "old.jpg" } }),
                registry,
                wallpaper,
                _ => picker,
                new FakeFolderPicker());

            await vm.InitializeAsync();
            vm.Monitors[0].FolderPath = folder;
            var applyTask = vm.Monitors[0].ApplyNowAsync();
            await wallpaper.FirstApplyStarted.Task;

            registry.SetConnectedMonitors("monitor-1", "monitor-2");
            var refreshTask = vm.RefreshAsync();
            Assert.False(refreshTask.IsCompleted);

            wallpaper.ReleaseFirstApply();
            await wallpaper.SecondApplyStarted.Task;

            Assert.Equal(1, picker.PickCount);
            Assert.Equal(2, vm.Monitors.Count);

            wallpaper.ReleaseSecondApply();
            await Task.WhenAll(applyTask, refreshTask);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAfterDisplayChangeAsync_persists_a_completed_apply_before_rebuilding_rows()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var imagePath = Path.Combine(folder, "chosen.jpg");
        await File.WriteAllTextAsync(imagePath, string.Empty);

        try
        {
            var settings = new FakeSettingsStore(new[] { new WallpaperMonitorProfile("monitor-1") { LastAppliedImage = "old.jpg" } });
            var wallpaper = new CoordinatedWallpaperService();
            var vm = new MainViewModel(
                settings,
                new FakeMonitorRegistry("monitor-1"),
                wallpaper,
                _ => new FakeImagePicker { ImageToReturn = imagePath },
                new FakeFolderPicker());

            await vm.InitializeAsync();
            vm.Monitors[0].FolderPath = folder;
            var applyTask = vm.Monitors[0].ApplyNowAsync();
            await wallpaper.FirstApplyStarted.Task;

            var refreshTask = vm.RefreshAfterDisplayChangeAsync();
            Assert.False(refreshTask.IsCompleted);

            wallpaper.ReleaseFirstApply();
            await wallpaper.SecondApplyStarted.Task;
            wallpaper.ReleaseSecondApply();
            await Task.WhenAll(applyTask, refreshTask);

            Assert.Equal(imagePath, settings.SavedProfiles!.Single().LastAppliedImage);
            Assert.Equal(imagePath, vm.Monitors[0].CurrentImagePath);
            Assert.Equal(imagePath, wallpaper.LastSnapshot!["monitor-1"]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_with_a_new_unconfigured_monitor_applies_the_known_image_snapshot()
    {
        var registry = new FakeMonitorRegistry("monitor-1");
        var wallpaper = new FakeWallpaperService();
        var vm = new MainViewModel(
            new FakeSettingsStore(new[] { new WallpaperMonitorProfile("monitor-1") { LastAppliedImage = "one.jpg" } }),
            registry,
            wallpaper,
            _ => new FakeImagePicker(),
            new FakeFolderPicker());

        await vm.InitializeAsync();
        registry.SetConnectedMonitors("monitor-1", "monitor-2");

        await vm.RefreshAsync();

        Assert.Equal(2, vm.Monitors.Count);
        Assert.Equal(1, wallpaper.ApplyCount);
        var snapshot = wallpaper.LastSnapshot;
        Assert.NotNull(snapshot);
        Assert.Single(snapshot);
        Assert.Equal("one.jpg", snapshot["monitor-1"]);
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
    public void Monitor_row_exposes_proposal_state_and_command()
    {
        var rowType = typeof(MonitorRowViewModel);

        Assert.NotNull(rowType.GetProperty("ProposedImagePath"));
        Assert.NotNull(rowType.GetProperty("ImageCount"));
        Assert.NotNull(rowType.GetProperty("ProposedImageFileName"));
        Assert.NotNull(rowType.GetProperty("ProposalStatus"));
        Assert.NotNull(rowType.GetProperty("NewProposalCommand"));
        Assert.NotNull(typeof(MainViewModel).GetProperty("NewProposalCommand"));
    }

    [Fact]
    public async Task BrowseFolderAsync_generates_a_proposal_without_applying_wallpaper()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var imagePath = Path.Combine(folder, "proposed.jpg");
        await File.WriteAllTextAsync(imagePath, string.Empty);

        try
        {
            var wallpaper = new FakeWallpaperService();
            var vm = new MainViewModel(
                new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
                new FakeMonitorRegistry("monitor-1"),
                wallpaper,
                _ => new FakeImagePicker { ImageToReturn = imagePath },
                new FakeFolderPicker { FolderToReturn = folder });
            await vm.InitializeAsync();

            await vm.Monitors.Single().BrowseFolderAsync();

            var row = vm.Monitors.Single();
            Assert.Equal(imagePath, row.ProposedImagePath);
            Assert.Equal("proposed.jpg", row.ProposedImageFileName);
            Assert.Equal(1, row.ImageCount);
            Assert.Equal("Proposed proposed.jpg for monitor-1.", row.ProposalStatus);
            Assert.Equal(row.ProposalStatus, vm.StatusMessage);
            Assert.Equal(imagePath, vm.VirtualMonitors.Single().ProposedImagePath);
            Assert.Equal(0, wallpaper.ApplyCount);
            Assert.Null(row.CurrentImagePath);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task NewProposalCommand_uses_the_selected_monitor()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var imagePath = Path.Combine(folder, "second.jpg");
        await File.WriteAllTextAsync(imagePath, string.Empty);

        try
        {
            var vm = new MainViewModel(
                new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
                new FakeMonitorRegistry("monitor-1", "monitor-2"),
                new FakeWallpaperService(),
                _ => new FakeImagePicker { ImageToReturn = imagePath },
                new FakeFolderPicker());
            await vm.InitializeAsync();
            vm.Monitors[1].FolderPath = folder;
            vm.SelectedVirtualMonitor = vm.VirtualMonitors[1];

            vm.NewProposalCommand.Execute(null);

            Assert.Null(vm.Monitors[0].ProposedImagePath);
            Assert.Equal(imagePath, vm.Monitors[1].ProposedImagePath);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Selected_monitor_row_tracks_the_monitor_selected_on_the_canvas()
    {
        var vm = new MainViewModel(
            new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
            new FakeMonitorRegistry("monitor-1", "monitor-2"),
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());
        await vm.InitializeAsync();

        vm.SelectedVirtualMonitor = vm.VirtualMonitors[1];

        Assert.Equal("monitor-2", vm.SelectedMonitorRow?.MonitorId);
    }

    [Fact]
    public async Task Folder_edits_immediately_replace_the_selected_row_proposal_state()
    {
        var imageFolder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        var emptyFolder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(imageFolder);
        Directory.CreateDirectory(emptyFolder);
        await File.WriteAllTextAsync(Path.Combine(imageFolder, "proposal.jpg"), string.Empty);

        try
        {
            var vm = new MainViewModel(
                new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
                new FakeMonitorRegistry("monitor-1"),
                new FakeWallpaperService(),
                _ => new FakeImagePicker(),
                new FakeFolderPicker());
            await vm.InitializeAsync();
            var row = vm.Monitors.Single();

            row.FolderPath = imageFolder;

            Assert.Equal(1, row.ImageCount);
            Assert.Null(row.ProposedImagePath);
            Assert.Null(row.ProposedImageFileName);
            Assert.Equal("1 images available for monitor-1.", row.ProposalStatus);
            Assert.True(row.CanCreateProposal);
            Assert.True(row.CanApplyProposal);

            row.NewProposalCommand.Execute(null);
            var virtualMonitor = vm.VirtualMonitors.Single();
            Assert.NotNull(virtualMonitor.ProposedImagePath);
            row.FolderPath = emptyFolder;

            Assert.Equal(0, row.ImageCount);
            Assert.Null(row.ProposedImagePath);
            Assert.Null(row.ProposedImageFileName);
            Assert.Null(virtualMonitor.ProposedImagePath);
            Assert.Null(virtualMonitor.PreviewImagePath);
            Assert.Equal($"No images found in {emptyFolder}.", row.ProposalStatus);
            Assert.False(row.CanCreateProposal);
            Assert.False(row.CanApplyProposal);
        }
        finally
        {
            Directory.Delete(imageFolder, recursive: true);
            Directory.Delete(emptyFolder, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, "Folder not found for monitor-1.")]
    [InlineData("", "Folder not found for monitor-1.")]
    public async Task NewProposalCommand_reports_missing_folders(string? folder, string expectedStatus)
    {
        var vm = new MainViewModel(
            new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
            new FakeMonitorRegistry("monitor-1"),
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());
        await vm.InitializeAsync();
        vm.Monitors.Single().FolderPath = folder;

        vm.NewProposalCommand.Execute(null);

        var row = vm.Monitors.Single();
        Assert.Equal(0, row.ImageCount);
        Assert.Null(row.ProposedImagePath);
        Assert.Equal(expectedStatus, row.ProposalStatus);
        Assert.Equal(expectedStatus, vm.StatusMessage);
    }

    [Fact]
    public async Task NewProposalCommand_reports_an_empty_folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            var vm = new MainViewModel(
                new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
                new FakeMonitorRegistry("monitor-1"),
                new FakeWallpaperService(),
                _ => new FakeImagePicker(),
                new FakeFolderPicker());
            await vm.InitializeAsync();
            vm.Monitors.Single().FolderPath = folder;

            vm.NewProposalCommand.Execute(null);

            var row = vm.Monitors.Single();
            Assert.Equal(0, row.ImageCount);
            Assert.Null(row.ProposedImagePath);
            Assert.Equal($"No images found in {folder}.", row.ProposalStatus);
            Assert.Equal(row.ProposalStatus, vm.StatusMessage);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task NewProposalCommand_reports_when_no_monitor_is_connected()
    {
        var vm = new MainViewModel(
            new FakeSettingsStore(Array.Empty<WallpaperMonitorProfile>()),
            new FakeMonitorRegistry(Array.Empty<string>()),
            new FakeWallpaperService(),
            _ => new FakeImagePicker(),
            new FakeFolderPicker());
        await vm.InitializeAsync();

        vm.NewProposalCommand.Execute(null);

        Assert.Equal("No monitor is available; connect a monitor and try again.", vm.StatusMessage);
    }

    [Fact]
    public async Task ApplyNowAsync_applies_the_selected_monitor_proposal_and_preserves_other_current_images()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var proposedImage = Path.Combine(folder, "proposed.jpg");
        var pickerImage = Path.Combine(folder, "picker.jpg");
        await File.WriteAllTextAsync(proposedImage, string.Empty);

        try
        {
            var wallpaper = new FakeWallpaperService();
            var vm = new MainViewModel(
                new FakeSettingsStore(new[]
                {
                    new WallpaperMonitorProfile("monitor-1") { LastAppliedImage = "old.jpg" },
                    new WallpaperMonitorProfile("monitor-2") { LastAppliedImage = "other.jpg" }
                }),
                new FakeMonitorRegistry("monitor-1", "monitor-2"),
                wallpaper,
                _ => new FakeImagePicker { ImageToReturn = pickerImage },
                new FakeFolderPicker());
            await vm.InitializeAsync();
            var row = vm.Monitors[0];
            row.FolderPath = folder;
            row.NewProposalCommand.Execute(null);

            await row.ApplyNowAsync();

            Assert.Equal(proposedImage, wallpaper.LastSnapshot!["monitor-1"]);
            Assert.Equal("other.jpg", wallpaper.LastSnapshot["monitor-2"]);
            Assert.Equal(proposedImage, row.CurrentImagePath);
            Assert.Null(row.ProposedImagePath);
            Assert.Equal("Applied proposed.jpg for monitor-1.", vm.StatusMessage);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyNowAsync_keeps_the_proposal_and_reports_the_error_when_applying_it_fails()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var proposedImage = Path.Combine(folder, "proposed.jpg");
        await File.WriteAllTextAsync(proposedImage, string.Empty);

        try
        {
            var vm = new MainViewModel(
                new FakeSettingsStore(new[] { new WallpaperMonitorProfile("monitor-1") { LastAppliedImage = "old.jpg" } }),
                new FakeMonitorRegistry("monitor-1"),
                new ThrowingWallpaperService(),
                _ => new FakeImagePicker(),
                new FakeFolderPicker());
            await vm.InitializeAsync();
            var row = vm.Monitors.Single();
            row.FolderPath = folder;
            row.NewProposalCommand.Execute(null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => row.ApplyNowAsync());

            Assert.Equal(proposedImage, row.ProposedImagePath);
            Assert.Equal("old.jpg", row.CurrentImagePath);
            Assert.Equal("apply failed", vm.StatusMessage);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
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
            Assert.Equal(imagePath, vm.VirtualMonitors.Single().CurrentImagePath);
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
            return Task.FromResult<IReadOnlyList<WallpaperMonitorProfile>>(SavedProfiles?.ToArray() ?? loadedProfiles);
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

        public FakeMonitorRegistry(params MonitorDescriptor[] monitors)
        {
            this.monitors = monitors;
        }

        public void SetConnectedMonitors(params string[] monitorIds)
        {
            monitors = monitorIds
                .Select((monitorId, index) => new MonitorDescriptor(monitorId, monitorId, index, 0, 1, 1, index == 0))
                .ToArray();
        }

        public void SetConnectedMonitors(params MonitorDescriptor[] monitors)
        {
            this.monitors = monitors;
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
