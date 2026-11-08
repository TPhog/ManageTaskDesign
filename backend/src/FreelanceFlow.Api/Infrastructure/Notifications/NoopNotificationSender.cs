namespace FreelanceFlow.Api.Infrastructure.Notifications;

public sealed class NoopNotificationSender : INotificationSender
{
    public Task SendAsync(Notification n, CancellationToken ct) => Task.CompletedTask;
}
