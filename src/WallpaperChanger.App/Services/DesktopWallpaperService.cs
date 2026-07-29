using WallpaperChanger.App.Interop;
using WallpaperChanger.Core.Abstractions;

namespace WallpaperChanger.App.Services;

public sealed class DesktopWallpaperService : IWallpaperService
{
    private readonly IDesktopWallpaperGateway wallpaperGateway;

    public DesktopWallpaperService(IDesktopWallpaperGateway wallpaperGateway)
    {
        this.wallpaperGateway = wallpaperGateway;
    }

    public Task SetWallpaperForMonitorAsync(string monitorId, string imagePath, CancellationToken cancellationToken = default)
    {
        return wallpaperGateway.SetWallpaperAsync(monitorId, imagePath);
    }
}
