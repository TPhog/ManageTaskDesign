using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FreelanceFlow.Api.Domain.Common;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Infrastructure.Notifications;

/// <summary>
/// POSTs an embed to the Discord webhook; one retry on 429 (honouring retry_after, max 5s) or a transient failure.
/// The webhook URL is a secret: it is never logged and exceptions are logged by type only.
/// </summary>
public sealed class DiscordWebhookSender(
    HttpClient http,
    IOptions<NotificationsSettings> options,
    IClock clock,
    ILogger<DiscordWebhookSender> logger) : INotificationSender
{
    private static readonly TimeSpan MaxRetryWait = TimeSpan.FromSeconds(5);

    internal Func<TimeSpan, CancellationToken, Task> DelayAsync { get; set; } = Task.Delay;

    public async Task SendAsync(Notification n, CancellationToken ct)
    {
        var url = options.Value.Discord.WebhookUrl;
        var payload = DiscordPayloadBuilder.Build(n, clock.UtcNow);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            TimeSpan wait;
            try
            {
                using var resp = await http.PostAsJsonAsync(url, payload, ct);
                if (resp.IsSuccessStatusCode) return;
                if (resp.StatusCode != HttpStatusCode.TooManyRequests && (int)resp.StatusCode < 500)
                {
                    logger.LogWarning("Discord webhook rejected the notification ({Status})", (int)resp.StatusCode);
                    return;
                }
                if (attempt == 2)
                {
                    logger.LogWarning("Discord webhook failed after retry ({Status})", (int)resp.StatusCode);
                    return;
                }
                wait = resp.StatusCode == HttpStatusCode.TooManyRequests ? await RetryAfterAsync(resp, ct) : TimeSpan.FromSeconds(1);
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
            {
                if (attempt == 2)
                {
                    logger.LogWarning("Discord webhook failed after retry ({ExceptionType})", ex.GetType().Name);
                    return;
                }
                wait = TimeSpan.FromSeconds(1);
            }
            await DelayAsync(wait, ct);
        }
    }

    private static async Task<TimeSpan> RetryAfterAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var seconds = 1.0;
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (body.TryGetProperty("retry_after", out var ra) && ra.TryGetDouble(out var v)) seconds = v;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException) { }
        var wait = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return wait > MaxRetryWait ? MaxRetryWait : wait;
    }
}
