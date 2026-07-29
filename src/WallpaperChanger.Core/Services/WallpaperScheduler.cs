using WallpaperChanger.Core.Models;

namespace WallpaperChanger.Core.Services;

public static class WallpaperScheduler
{
    public static DateTimeOffset GetNextRun(DateTimeOffset now, WallpaperMonitorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.IntervalUnit);

        return profile.IntervalUnit.Trim().ToLowerInvariant() switch
        {
            "minute" or "minutes" => now.AddMinutes(profile.IntervalValue),
            "hour" or "hours" => now.AddHours(profile.IntervalValue),
            "day" or "days" => now.AddDays(profile.IntervalValue),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), $"Unsupported interval unit '{profile.IntervalUnit}'.")
        };
    }
}
