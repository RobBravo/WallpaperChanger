using WallpaperChanger.Core.Models;

namespace WallpaperChanger.Core.Abstractions;

public interface ISettingsStore
{
    Task<IReadOnlyList<WallpaperMonitorProfile>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyCollection<WallpaperMonitorProfile> profiles, CancellationToken cancellationToken = default);
}
