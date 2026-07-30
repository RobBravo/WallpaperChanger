using WallpaperChanger.Core.Models;

namespace WallpaperChanger.App.ViewModels;

public sealed class VirtualMonitorViewModel : ObservableObject
{
    private string? currentImagePath;

    public VirtualMonitorViewModel(
        MonitorDescriptor monitor,
        double normalizedLeft,
        double normalizedTop,
        double normalizedWidth,
        double normalizedHeight,
        string? currentImagePath = null,
        double layoutAspectRatio = 1)
    {
        MonitorId = monitor.Id;
        NormalizedLeft = normalizedLeft;
        NormalizedTop = normalizedTop;
        NormalizedWidth = normalizedWidth;
        NormalizedHeight = normalizedHeight;
        IsPortrait = monitor.Height > monitor.Width;
        this.currentImagePath = currentImagePath;
        LayoutAspectRatio = layoutAspectRatio;
    }

    public string MonitorId { get; }

    public double NormalizedLeft { get; }

    public double NormalizedTop { get; }

    public double NormalizedWidth { get; }

    public double NormalizedHeight { get; }

    public bool IsPortrait { get; }

    public double LayoutAspectRatio { get; }

    public string? CurrentImagePath
    {
        get => currentImagePath;
        internal set => SetProperty(ref currentImagePath, value);
    }
}
