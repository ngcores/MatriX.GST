using MatriX.GST.Services;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MatriX.GST.Background;

public class TorCleanupService : BackgroundService
{
    readonly TorManager torManager;

    public TorCleanupService(TorManager torManager)
    {
        this.torManager = torManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await torManager.CleanupAsync();

            try
            {
                await Task.Delay(20_000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
