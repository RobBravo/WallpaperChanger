using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WallpaperChanger.App.ViewModels;

namespace WallpaperChanger.App.Views;

public partial class SelectedMonitorPanel : System.Windows.Controls.UserControl
{
    private int previewVersion;

    public SelectedMonitorPanel()
    {
        InitializeComponent();
    }

    private async void ProposalPathUpdated(object sender, DataTransferEventArgs e)
    {
        var version = ++previewVersion;
        var imagePath = (DataContext as MonitorRowViewModel)?.ProposedImagePath;
        ProposalPreview.Source = null;

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        try
        {
            var preview = await Task.Run(() => LoadPreview(imagePath));
            if (version == previewVersion)
            {
                ProposalPreview.Source = preview;
            }
        }
        catch (Exception) when (version == previewVersion)
        {
            ProposalPreview.Source = null;
        }
    }

    private static ImageSource LoadPreview(string imagePath)
    {
        var preview = new BitmapImage();
        preview.BeginInit();
        preview.UriSource = new Uri(imagePath, UriKind.Absolute);
        preview.DecodePixelWidth = 480;
        preview.CacheOption = BitmapCacheOption.OnLoad;
        preview.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        preview.EndInit();
        preview.Freeze();
        return preview;
    }
}
