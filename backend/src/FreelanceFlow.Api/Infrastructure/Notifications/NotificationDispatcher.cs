namespace FreelanceFlow.Api.Infrastructure.Notifications;

public sealed class NotificationDispatcher(
    NotificationQueue queue,
    INotificationSender sender,
    ILogger<NotificationDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var n in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await sender.SendAsync(n, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // type only: messages of transport exceptions may echo the webhook URL
                    logger.LogWarning("Notification delivery failed ({ExceptionType})", ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
