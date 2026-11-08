namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>
/// P11 BR-94: wakes up every Digest:CheckIntervalSeconds and lets <see cref="DailyDigestService"/> decide whether today's
/// digest is due. Registered only when the interval is &gt; 0 (tests set it to 0 and call the service directly).
/// </summary>
public sealed class DailyDigestHostedService(
    IServiceScopeFactory scopes, IConfiguration config, ILogger<DailyDigestHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(config.GetValue("Digest:CheckIntervalSeconds", 60)));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DailyDigestService>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("Daily digest check failed ({ExceptionType})", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
