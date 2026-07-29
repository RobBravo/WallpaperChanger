namespace WallpaperChanger.Core.Models;

public sealed record MonitorDescriptor(
    string Id,
    string DeviceName,
    int Left,
    int Top,
    int Width,
    int Height,
    bool IsPrimary);
