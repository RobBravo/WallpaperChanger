using WallpaperChanger.App.Interop;
using WallpaperChanger.Core.Abstractions;

namespace WallpaperChanger.App.Services;

public sealed class WindowsMonitorRegistry : IMonitorRegistry
{
    public IReadOnlyList<string> GetConnectedMonitorIds()
    {
        var wallpaper = DesktopWallpaperComFactory.Create();
        wallpaper.GetMonitorDevicePathCount(out var count);

        var monitorIds = new string[count];
        for (var index = 0; index < count; index++)
        {
            wallpaper.GetMonitorDevicePathAt((uint)index, out var monitorId);
            monitorIds[index] = monitorId;
        }

        return monitorIds;
    }
}
