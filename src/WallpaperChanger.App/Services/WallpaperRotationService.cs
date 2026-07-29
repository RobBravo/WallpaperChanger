using System.Windows.Threading;
using WallpaperChanger.App.ViewModels;
using WallpaperChanger.Core.Abstractions;

namespace WallpaperChanger.App.Services;

public sealed class WallpaperRotationService : IDisposable
{
    private readonly MainViewModel viewModel;
    private readonly IClock clock;
    private readonly DispatcherTimer timer;
    private bool isTickRunning;

    public WallpaperRotationService(MainViewModel viewModel, IClock clock)
    {
        this.viewModel = viewModel;
        this.clock = clock;
        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        timer.Tick += OnTick;
    }

    public void Start()
    {
        timer.Start();
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= OnTick;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (isTickRunning)
        {
            return;
        }

        isTickRunning = true;

        try
        {
            var now = clock.UtcNow;
            var dueRows = viewModel.Monitors.Where(row => row.NextRunAt <= now).ToArray();

            foreach (var row in dueRows)
            {
                try
                {
                    await row.ApplyNowAsync();
                }
                catch (Exception ex)
                {
                    viewModel.ReportError(ex);
                }
            }
        }
        finally
        {
            isTickRunning = false;
        }
    }
}
