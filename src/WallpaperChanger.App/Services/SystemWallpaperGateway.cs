using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WallpaperChanger.App.Services;

public interface ISystemWallpaperGateway
{
    void Apply(string bitmapPath);
}

public sealed class SystemWallpaperGateway : ISystemWallpaperGateway
{
    public void Apply(string bitmapPath)
    {
        using var desktopKey = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop", writable: true);
        desktopKey.SetValue("WallpaperStyle", "22", RegistryValueKind.String);
        desktopKey.SetValue("TileWallpaper", "0", RegistryValueKind.String);

        if (!SystemParametersInfo(0x0014, 0, bitmapPath, 0x0001 | 0x0002))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, string value, uint updateFlags);
}
