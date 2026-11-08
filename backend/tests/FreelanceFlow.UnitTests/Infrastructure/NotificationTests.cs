using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.UnitTests.Infrastructure;

public class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);
    private const string Secret = "https://discord.test/api/webhooks/123/SECRET-TOKEN";

    [Fact]
    public void Payload_truncates_description_sets_color_fields_and_url()
    {
        var n = new Notification("T", new string('a', 3000), "https://app.test/x", NotificationSeverity.Critical,
            [new NotificationField("Khách", "An")]);

        var embed = DiscordPayloadBuilder.Build(n, Now)["embeds"]![0]!;

        ((string)embed["description"]!).Length.Should().Be(2000);
        ((string)embed["description"]!).Should().EndWith("…");
        ((int)embed["color"]!).Should().Be(0xE74C3C);
        ((string)embed["url"]!).Should().Be("https://app.test/x");
        ((string)embed["timestamp"]!).Should().StartWith("2026-11-08T03:00:00");
        ((string)embed["fields"]![0]!["name"]!).Should().Be("Khách");
        ((string)embed["fields"]![0]!["value"]!).Should().Be("An");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/admin/requests?id=1")]
    public void Payload_omits_url_when_absent_or_relative(string? url)
    {
        var embed = DiscordPayloadBuilder.Build(new Notification("T", "D", url), Now)["embeds"]![0]!.AsObject();
        embed.ContainsKey("url").Should().BeFalse();
        embed.ContainsKey("fields").Should().BeFalse();
    }

    [Theory]
    [InlineData(NotificationSeverity.Info, 0x3498DB)]
    [InlineData(NotificationSeverity.Warning, 0xF1C40F)]
    [InlineData(NotificationSeverity.High, 0xE67E22)]
    public void Severity_maps_to_color(NotificationSeverity s, int color) =>
        DiscordPayloadBuilder.Color(s).Should().Be(color);

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
        public DateOnly TodayVn => DateOnly.FromDateTime(Now.UtcDateTime);
    }

    private sealed class StubHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public List<(Uri? Uri, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add((request.RequestUri, await request.Content!.ReadAsStringAsync(ct)));
            return responses[Math.Min(Calls.Count, responses.Length) - 1](request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (DiscordWebhookSender Sender, List<TimeSpan> Delays) CreateSender(StubHandler handler)
    {
        var delays = new List<TimeSpan>();
        var sender = new DiscordWebhookSender(new HttpClient(handler),
            Options.Create(new NotificationsSettings { Discord = new() { WebhookUrl = Secret } }),
            new FakeClock(), NullLogger<DiscordWebhookSender>.Instance)
        {
            DelayAsync = (d, _) => { delays.Add(d); return Task.CompletedTask; },
        };
        return (sender, delays);
    }

    [Fact]
    public async Task Sender_posts_discord_json_to_the_webhook()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var (sender, _) = CreateSender(handler);

        await sender.SendAsync(new Notification("Tiêu đề", "Nội dung", Severity: NotificationSeverity.Warning), default);

        handler.Calls.Should().ContainSingle();
        handler.Calls[0].Uri!.ToString().Should().Be(Secret);
        var json = JsonDocument.Parse(handler.Calls[0].Body).RootElement;
        json.GetProperty("username").GetString().Should().Be("FreelanceFlow");
        var embed = json.GetProperty("embeds")[0];
        embed.GetProperty("title").GetString().Should().Be("Tiêu đề");
        embed.GetProperty("color").GetInt32().Should().Be(0xF1C40F);
    }

    [Fact]
    public async Task Sender_retries_once_on_429_honouring_retry_after_capped_at_5s()
    {
        var handler = new StubHandler(
            _ => Json(HttpStatusCode.TooManyRequests, "{\"retry_after\": 30.0}"),
            _ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var (sender, delays) = CreateSender(handler);

        await sender.SendAsync(new Notification("T", "D"), default);

        handler.Calls.Should().HaveCount(2);
        delays.Should().ContainSingle().Which.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Sender_gives_up_after_one_retry_without_throwing()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "{}"));
        var (sender, _) = CreateSender(handler);

        await sender.SendAsync(new Notification("T", "D"), default);

        handler.Calls.Should().HaveCount(2);
    }

    private sealed class FakeSender(Func<Notification, Task> onSend) : INotificationSender
    {
        public Task SendAsync(Notification n, CancellationToken ct) => onSend(n);
    }

    [Fact]
    public async Task Dispatcher_sends_enqueued_notifications_and_survives_a_throwing_sender()
    {
        var queue = new NotificationQueue();
        var sent = new List<string>();
        var done = new TaskCompletionSource();
        var sender = new FakeSender(n =>
        {
            if (n.Title == "boom") throw new InvalidOperationException(Secret);
            sent.Add(n.Title);
            if (n.Title == "after") done.SetResult();
            return Task.CompletedTask;
        });
        var dispatcher = new NotificationDispatcher(queue, sender, NullLogger<NotificationDispatcher>.Instance);
        await dispatcher.StartAsync(default);

        await queue.EnqueueAsync(new Notification("first", "d"));
        await queue.EnqueueAsync(new Notification("boom", "d"));
        await queue.EnqueueAsync(new Notification("after", "d"));

        (await Task.WhenAny(done.Task, Task.Delay(5000))).Should().Be(done.Task);
        sent.Should().Equal("first", "after");
        await dispatcher.StopAsync(default);
    }
}
