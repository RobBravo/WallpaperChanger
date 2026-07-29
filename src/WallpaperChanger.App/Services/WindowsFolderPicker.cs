using System.Windows.Forms;
using WallpaperChanger.App.ViewModels;

namespace WallpaperChanger.App.Services;

public sealed class WindowsFolderPicker : IFolderPicker
{
    public string? PickFolder(string? initialFolder)
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = string.IsNullOrWhiteSpace(initialFolder) ? Environment.CurrentDirectory : initialFolder,
            UseDescriptionForTitle = true,
            Description = "Select a wallpaper folder"
        };

        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }
}
