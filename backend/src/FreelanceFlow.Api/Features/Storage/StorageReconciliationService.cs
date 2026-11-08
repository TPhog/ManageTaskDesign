using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Features.Storage;

/// <summary>
/// Spec §2: once per Storage:ReconcileIntervalHours compares the bytes in storage with the database (the source of
/// truth — drift is only logged) and re-evaluates the thresholds. Registered only when the interval is &gt; 0.
/// </summary>
public sealed class StorageReconciliationService(
    IServiceScopeFactory scopes, IOptions<StorageOptions> options, ILogger<StorageReconciliationService> logger) : BackgroundService
{
    private const long DriftToleranceBytes = 1_048_576;

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
        var alerts = scope.ServiceProvider.GetRequiredService<StorageAlertService>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var dbBytes = await alerts.UsedBytesAsync(ct);
        var storedBytes = await storage.TotalBytesAsync("", ct);
        if (Math.Abs(storedBytes - dbBytes) > DriftToleranceBytes)
            logger.LogWarning("Storage drift: provider holds {StoredBytes} bytes, database counts {DbBytes}", storedBytes, dbBytes);
        await alerts.EvaluateAsync(ct);
    }
}
