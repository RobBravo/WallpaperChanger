using Xunit;
using WallpaperChanger.Core.Models;
using WallpaperChanger.Core.Services;

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
}
