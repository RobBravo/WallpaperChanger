using WallpaperChanger.Core.Models;

namespace WallpaperChanger.Core.Abstractions;

public interface IMonitorRegistry
{
    IReadOnlyList<MonitorDescriptor> GetConnectedMonitors();
}
