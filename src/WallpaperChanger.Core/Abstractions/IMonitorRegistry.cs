namespace WallpaperChanger.Core.Abstractions;

public interface IMonitorRegistry
{
    IReadOnlyList<string> GetConnectedMonitorIds();
}
