namespace WallpaperChanger.Core.Models;

public sealed class WallpaperMonitorProfile
{
    public WallpaperMonitorProfile(string monitorId)
    {
        MonitorId = monitorId;
        IntervalValue = 1;
        IntervalUnit = "minutes";
    }

    public string MonitorId { get; }

    public string? FolderPath { get; set; }

    public int IntervalValue { get; set; }

    public string IntervalUnit { get; set; }

    public string? LastAppliedImage { get; set; }

    public IReadOnlyList<string> RemainingImages { get; set; } = Array.Empty<string>();
}
