using System.Windows;
using WallpaperChanger.App.Views;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class MonitorCanvasPreviewStateTests
{
    [Fact]
    public void Preview_failure_state_is_cleared_when_a_later_source_is_assigned()
    {
        var target = new DependencyObject();

        MonitorCanvasView.MarkPreviewLoadFailed(target);
        Assert.True(MonitorCanvasView.GetPreviewLoadFailed(target));

        MonitorCanvasView.ResetPreviewLoadFailed(target);

        Assert.False(MonitorCanvasView.GetPreviewLoadFailed(target));
    }
}
