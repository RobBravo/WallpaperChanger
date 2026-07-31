using System.Runtime.InteropServices;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Models;

namespace WallpaperChanger.App.Services;

public sealed class WindowsMonitorRegistry : IMonitorRegistry
{
    public IReadOnlyList<MonitorDescriptor> GetConnectedMonitors()
    {
        // Monitor rectangles are only reported in true physical pixels when the calling
        // thread is per-monitor-v2 DPI aware. The process-wide manifest setting does not
        // reliably propagate to background threads (e.g. the rotation timer's Task.Run
        // thread), so this must be set on every thread that enumerates monitors.
        SetThreadDpiAwarenessContext(new IntPtr(-4));

        var monitors = new List<MonitorDescriptor>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var monitorInfo = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                return true;
            }

            var displayDevice = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            var hasDeviceInterface = EnumDisplayDevices(monitorInfo.DeviceName, 0, ref displayDevice, 0x00000001);
            var monitorId = hasDeviceInterface && !string.IsNullOrWhiteSpace(displayDevice.DeviceId)
                ? displayDevice.DeviceId
                : monitorInfo.DeviceName;

            monitors.Add(new MonitorDescriptor(
                monitorId,
                monitorInfo.DeviceName,
                monitorInfo.Monitor.Left,
                monitorInfo.Monitor.Top,
                monitorInfo.Monitor.Right - monitorInfo.Monitor.Left,
                monitorInfo.Monitor.Bottom - monitorInfo.Monitor.Top,
                (monitorInfo.Flags & 0x00000001) != 0));

            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr deviceContext, IntPtr monitorRectangle, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumDisplayMonitors(IntPtr deviceContext, IntPtr clipRectangle, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumDisplayDevices(string deviceName, uint deviceIndex, ref DisplayDevice displayDevice, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rectangle Monitor;
        public Rectangle Work;
        public int Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public int StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }
}
