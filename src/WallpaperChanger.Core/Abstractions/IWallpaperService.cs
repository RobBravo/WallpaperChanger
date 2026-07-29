namespace WallpaperChanger.Core.Abstractions;

public interface IWallpaperService
{
    Task SetWallpaperForMonitorAsync(string monitorId, string imagePath, CancellationToken cancellationToken = default);
}
