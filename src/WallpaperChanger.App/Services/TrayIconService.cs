using System.Drawing;
using System.Windows.Forms;

namespace WallpaperChanger.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon notifyIcon;

    public TrayIconService(Action openWindow, Action exitApplication)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => openWindow());
        menu.Items.Add("Exit", null, (_, _) => exitApplication());

        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
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
        notifyIcon.Dispose();
    }
}
