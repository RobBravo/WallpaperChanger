namespace WallpaperChanger.Core.Abstractions;

public interface IWallpaperService
{
    Task ApplyAsync(IReadOnlyDictionary<string, string> imagePathsByMonitorId, CancellationToken cancellationToken = default);
}
