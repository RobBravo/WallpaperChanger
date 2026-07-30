using WallpaperChanger.Core.Models;

namespace WallpaperChanger.App.ViewModels;

public sealed class VirtualMonitorViewModel
{
    public VirtualMonitorViewModel(
        MonitorDescriptor monitor,
        double normalizedLeft,
        double normalizedTop,
        double normalizedWidth,
        double normalizedHeight)
    {
        MonitorId = monitor.Id;
        NormalizedLeft = normalizedLeft;
        NormalizedTop = normalizedTop;
        NormalizedWidth = normalizedWidth;
        NormalizedHeight = normalizedHeight;
        IsPortrait = monitor.Height > monitor.Width;
    }

    public string MonitorId { get; }

    public double NormalizedLeft { get; }

    public double NormalizedTop { get; }

    public double NormalizedWidth { get; }

    public double NormalizedHeight { get; }

    public bool IsPortrait { get; }
}
