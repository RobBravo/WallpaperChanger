using System.Drawing;
using System.Windows.Forms;

namespace WallpaperChanger.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon notifyIcon;

    public TrayIconService(Action openWindow, Func<Task> exitApplication)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => openWindow());
        menu.Items.Add("Exit", null, async (_, _) => await exitApplication());

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
        notifyIcon.ContextMenuStrip?.Dispose();
        notifyIcon.Dispose();
    }
}
