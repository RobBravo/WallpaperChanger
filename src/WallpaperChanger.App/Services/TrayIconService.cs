using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WallpaperChanger.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon notifyIcon;
    private readonly Stream iconStream;

    public TrayIconService(Action openWindow, Func<Task> exitApplication)
    {
        var iconResource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/WallpaperChanger.ico", UriKind.Absolute));
        iconStream = iconResource?.Stream
            ?? throw new InvalidOperationException("Could not load the application icon.");

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => openWindow());
        menu.Items.Add("Exit", null, async (_, _) => await exitApplication());

        notifyIcon = new NotifyIcon
        {
            Icon = new Icon(iconStream),
            Text = "WallpaperChanger",
            Visible = true,
            ContextMenuStrip = menu
        };

        notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                openWindow();
            }
        };
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.ContextMenuStrip?.Dispose();
        notifyIcon.Dispose();
        iconStream.Dispose();
    }
}
