namespace WallpaperChanger.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
