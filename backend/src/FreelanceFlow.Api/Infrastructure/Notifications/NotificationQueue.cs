using System.Threading.Channels;

namespace FreelanceFlow.Api.Infrastructure.Notifications;

/// <summary>Bounded in-memory queue; when full the oldest pending alert is dropped.</summary>
public sealed class NotificationQueue : INotifier
{
    private readonly Channel<Notification> _channel = Channel.CreateBounded<Notification>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<Notification> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Notification n, CancellationToken ct = default)
    {
        _channel.Writer.TryWrite(n);
        return ValueTask.CompletedTask;
    }
}
