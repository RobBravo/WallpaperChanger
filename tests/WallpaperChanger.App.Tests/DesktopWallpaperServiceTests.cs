using WallpaperChanger.App.Interop;
using WallpaperChanger.App.Services;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class DesktopWallpaperServiceTests
{
    [Fact]
    public async Task Forwards_each_snapshot_image_to_wallpaper_gateway()
    {
        var gateway = new FakeWallpaperGateway();
        var service = new DesktopWallpaperService(gateway);

        await service.ApplyAsync(
            new Dictionary<string, string>
            {
                ["\\\\?\\DISPLAY1"] = "C:\\Images\\a.jpg"
            });

        Assert.NotNull(gateway.LastCall);
        Assert.Equal(("\\\\?\\DISPLAY1", "C:\\Images\\a.jpg"), gateway.LastCall.Value);
    }

    private sealed class FakeWallpaperGateway : IDesktopWallpaperGateway
    {
        public (string MonitorId, string ImagePath)? LastCall { get; private set; }

        public Task SetWallpaperAsync(string monitorId, string imagePath)
        {
            LastCall = (monitorId, imagePath);
            return Task.CompletedTask;
        }
    }
}
