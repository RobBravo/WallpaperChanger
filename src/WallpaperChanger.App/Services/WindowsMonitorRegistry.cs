using System.Windows.Forms;
using WallpaperChanger.Core.Abstractions;

namespace WallpaperChanger.App.Services;

public sealed class WindowsMonitorRegistry : IMonitorRegistry
{
    public IReadOnlyList<string> GetConnectedMonitorIds()
    {
        return Screen.AllScreens
            .Select(screen => screen.DeviceName)
            .ToArray();
    }
}
