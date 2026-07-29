using Xunit;
using WallpaperChanger.Core.Models;
using WallpaperChanger.Core.Services;
using System.Text.Json;

namespace WallpaperChanger.Core.Tests;

public class JsonSettingsStoreTests
{
    [Fact]
    public async Task Saves_and_loads_profiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}.json");
        var store = new JsonSettingsStore(path);
        var profiles = new[]
        {
            new WallpaperMonitorProfile("monitor-1")
            {
                FolderPath = "C:/Wallpapers/Monitor1",
                IntervalValue = 15,
                IntervalUnit = "minutes"
            },
            new WallpaperMonitorProfile("monitor-2")
            {
                FolderPath = "C:/Wallpapers/Monitor2",
                IntervalValue = 1,
                IntervalUnit = "hours"
            }
        };

        try
        {
            await store.SaveAsync(profiles);

            var loaded = await store.LoadAsync();

            Assert.Equal(2, loaded.Count);
            Assert.Equal("monitor-1", loaded[0].MonitorId);
            Assert.Equal("C:/Wallpapers/Monitor1", loaded[0].FolderPath);
            Assert.Equal(15, loaded[0].IntervalValue);
            Assert.Equal("minutes", loaded[0].IntervalUnit);
            Assert.Equal("monitor-2", loaded[1].MonitorId);
            Assert.Equal("C:/Wallpapers/Monitor2", loaded[1].FolderPath);
            Assert.Equal(1, loaded[1].IntervalValue);
            Assert.Equal("hours", loaded[1].IntervalUnit);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Loads_empty_list_when_file_is_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}.json");
        var store = new JsonSettingsStore(path);

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task Throws_when_file_is_corrupted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{not-json");
        var store = new JsonSettingsStore(path);

        try
        {
            await Assert.ThrowsAsync<JsonException>(() => store.LoadAsync());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Saves_and_loads_shuffle_state()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}.json");
        var store = new JsonSettingsStore(path);
        var profiles = new[]
        {
            new WallpaperMonitorProfile("monitor-1")
            {
                FolderPath = "C:/Wallpapers/Monitor1",
                IntervalValue = 15,
                IntervalUnit = "minutes",
                LastAppliedImage = "C:/Wallpapers/Monitor1/chosen.jpg",
                RemainingImages = new[] { "C:/Wallpapers/Monitor1/next.jpg" },
                NextRunAt = new DateTimeOffset(2026, 7, 28, 11, 0, 0, TimeSpan.Zero)
            }
        };

        try
        {
            await store.SaveAsync(profiles);

            var loaded = await store.LoadAsync();

            Assert.Single(loaded);
            Assert.Equal("C:/Wallpapers/Monitor1/chosen.jpg", loaded[0].LastAppliedImage);
            Assert.Equal(new[] { "C:/Wallpapers/Monitor1/next.jpg" }, loaded[0].RemainingImages);
            Assert.Equal(new DateTimeOffset(2026, 7, 28, 11, 0, 0, TimeSpan.Zero), loaded[0].NextRunAt);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
