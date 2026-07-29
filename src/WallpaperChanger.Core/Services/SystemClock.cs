using WallpaperChanger.Core.Abstractions;

namespace WallpaperChanger.Core.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
