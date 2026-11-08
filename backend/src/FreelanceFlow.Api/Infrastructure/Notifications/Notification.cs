namespace FreelanceFlow.Api.Infrastructure.Notifications;

public enum NotificationSeverity { Info, Warning, High, Critical }

public sealed record NotificationField(string Name, string Value);

/// <summary>A channel-agnostic alert for the designer. Never put secrets or portal tokens in it.</summary>
public sealed record Notification(
    string Title,
    string Description,
    string? Url = null,
    NotificationSeverity Severity = NotificationSeverity.Info,
    IReadOnlyList<NotificationField>? Fields = null);

public interface INotifier
{
    /// <summary>Fire-and-forget: queues the alert and returns immediately; delivery failures are only logged.</summary>
    ValueTask EnqueueAsync(Notification n, CancellationToken ct = default);
}

public interface INotificationSender
{
    Task SendAsync(Notification n, CancellationToken ct);
}

public sealed class NotificationsSettings
{
    public DiscordSettings Discord { get; init; } = new();

    public sealed class DiscordSettings
    {
        public string WebhookUrl { get; init; } = "";
    }
}
