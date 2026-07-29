using WallpaperChanger.App.Interop;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Models;

namespace WallpaperChanger.App.Services;

public sealed class WindowsMonitorRegistry : IMonitorRegistry
{
    public IReadOnlyList<MonitorDescriptor> GetConnectedMonitors()
    {
        var wallpaper = DesktopWallpaperComFactory.Create();
        wallpaper.GetMonitorDevicePathCount(out var count);

        var monitors = new MonitorDescriptor[count];
        for (var index = 0; index < count; index++)
        {
            wallpaper.GetMonitorDevicePathAt((uint)index, out var monitorId);
            monitors[index] = new MonitorDescriptor(monitorId, monitorId, 0, 0, 0, 0, index == 0);
        }

        return monitors;
    }
}
