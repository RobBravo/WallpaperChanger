using Xunit;
using WallpaperChanger.Core.Models;
using WallpaperChanger.Core.Services;

namespace WallpaperChanger.Core.Tests;

public class WallpaperSchedulerTests
{
    [Fact]
    public void Gets_next_run_from_now_plus_interval()
    {
        var now = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var profile = new WallpaperMonitorProfile("monitor-1")
        {
            IntervalValue = 30,
            IntervalUnit = "minutes"
        };

        var next = WallpaperScheduler.GetNextRun(now, profile);

        Assert.Equal(new DateTimeOffset(2026, 7, 28, 10, 30, 0, TimeSpan.Zero), next);
    }
}
