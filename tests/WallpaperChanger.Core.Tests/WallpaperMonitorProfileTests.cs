using Xunit;
using WallpaperChanger.Core.Models;

namespace WallpaperChanger.Core.Tests;

public class WallpaperMonitorProfileTests
{
    [Fact]
    public void Profile_initializes_with_expected_defaults()
    {
        var profile = new WallpaperMonitorProfile("monitor-1");

        Assert.Equal("monitor-1", profile.MonitorId);
        Assert.Equal(1, profile.IntervalValue);
        Assert.Equal("minutes", profile.IntervalUnit);
        Assert.Null(profile.FolderPath);
        Assert.Null(profile.LastAppliedImage);
        Assert.Empty(profile.RemainingImages);
        Assert.Null(profile.NextRunAt);
    }
}
