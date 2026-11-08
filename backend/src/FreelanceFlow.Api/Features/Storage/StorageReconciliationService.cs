using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Features.Storage;

/// <summary>
/// Spec §2 + P10: once per Storage:ReconcileIntervalHours runs StorageSweeper (repairs drift, logs counts) and
/// re-evaluates the thresholds. Registered only when the interval is &gt; 0.
/// </summary>
public sealed class StorageReconciliationService(
    IServiceScopeFactory scopes, IOptions<StorageOptions> options, ILogger<StorageReconciliationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.Value.ReconcileIntervalHours));
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("Storage reconciliation failed ({ExceptionType})", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<StorageSweeper>().RunAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // the threshold re-check below still runs
            logger.LogError("Storage sweep failed ({ExceptionType})", ex.GetType().Name);
        }
        await scope.ServiceProvider.GetRequiredService<StorageAlertService>().EvaluateAsync(ct);
    }
}
