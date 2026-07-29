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
        var wallpaper = (IDesktopWallpaperCom)new DesktopWallpaperComClass();
        wallpaper.SetWallpaper(monitorId, imagePath);

        return Task.CompletedTask;
    }
}

[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaperCom
{
    void SetWallpaper(
        [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
        [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
}

[ComImport]
[Guid("C2CF3110-460E-4FC1-B9D0-8A24A0C31775")]
internal sealed class DesktopWallpaperComClass
{
}
