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

    public async Task ApplyAsync(IReadOnlyDictionary<string, string> imagePathsByMonitorId, CancellationToken cancellationToken = default)
    {
        foreach (var (monitorId, imagePath) in imagePathsByMonitorId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await wallpaperGateway.SetWallpaperAsync(monitorId, imagePath);
        }
    }
}
