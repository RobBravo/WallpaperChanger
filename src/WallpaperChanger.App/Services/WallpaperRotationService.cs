using System.Windows;
using WallpaperChanger.App.ViewModels;
using WallpaperChanger.Core.Abstractions;

namespace WallpaperChanger.App.Services;

public sealed class WallpaperRotationService : IDisposable
{
    private readonly MainViewModel viewModel;
    private readonly IClock clock;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private Task? loopTask;

    public WallpaperRotationService(MainViewModel viewModel, IClock clock)
    {
        this.viewModel = viewModel;
        this.clock = clock;
    }

    public void Start()
    {
        loopTask ??= Task.Run(RunAsync);
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationTokenSource.Token))
            {
                var now = clock.UtcNow;
                var dueRows = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    viewModel.Monitors.Where(row => row.NextRunAt <= now).ToArray()).Task;

                foreach (var row in dueRows)
                {
                    try
                    {
                        await row.ApplyIfDueAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        viewModel.ReportError(ex);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
