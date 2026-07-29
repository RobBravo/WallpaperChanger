using System.Runtime.InteropServices;

namespace WallpaperChanger.App.Interop;

public interface IDesktopWallpaperGateway
{
    Task SetWallpaperAsync(string monitorId, string imagePath);
}

public sealed class DesktopWallpaper : IDesktopWallpaperGateway
{
    public Task SetWallpaperAsync(string monitorId, string imagePath)
    {
        var wallpaper = DesktopWallpaperComFactory.Create();
        wallpaper.SetWallpaper(monitorId, imagePath);

        return Task.CompletedTask;
    }
}

[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaperCom
{
    void SetWallpaper(
        [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
        [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    void GetWallpaper(
        [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
        [MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);

    void GetMonitorDevicePathAt(uint monitorIndex, [MarshalAs(UnmanagedType.LPWStr)] out string monitorDevicePath);

    void GetMonitorDevicePathCount(out uint count);
}

internal static class DesktopWallpaperComFactory
{
    private static readonly Guid ClassId = new("C2CF3110-460E-4FC1-B9D0-8A24A0C31775");

    public static IDesktopWallpaperCom Create()
    {
        var type = Type.GetTypeFromCLSID(ClassId, throwOnError: true)
            ?? throw new InvalidOperationException("The desktop wallpaper COM class is unavailable.");

        return (IDesktopWallpaperCom)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("The desktop wallpaper COM object could not be created."));
    }
}
