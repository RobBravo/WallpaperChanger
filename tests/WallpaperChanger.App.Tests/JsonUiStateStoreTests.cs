using WallpaperChanger.App.Services;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class JsonUiStateStoreTests
{
    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_selected_monitor_id()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "ui-state.json");

        try
        {
            var store = new JsonUiStateStore(path);

            await store.SaveAsync(new UiState("monitor-2"));

            Assert.Equal("monitor-2", (await store.LoadAsync()).SelectedMonitorId);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_returns_an_empty_state_when_the_file_does_not_exist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wallpaperchanger-{Guid.NewGuid():N}", "ui-state.json");
        var store = new JsonUiStateStore(path);

        var state = await store.LoadAsync();

        Assert.Null(state.SelectedMonitorId);
    }
}
