using System.Text.Json.Nodes;

namespace FreelanceFlow.Api.Infrastructure.Notifications;

public static class DiscordPayloadBuilder
{
    public const int MaxDescription = 2000;

    public static int Color(NotificationSeverity s) => s switch
    {
        NotificationSeverity.Warning => 0xF1C40F,
        NotificationSeverity.High => 0xE67E22,
        NotificationSeverity.Critical => 0xE74C3C,
        _ => 0x3498DB,
    };

    public static JsonObject Build(Notification n, DateTimeOffset now)
    {
        var embed = new JsonObject
        {
            ["title"] = Truncate(n.Title, 256),
            ["description"] = Truncate(n.Description, MaxDescription),
            ["color"] = Color(n.Severity),
            ["timestamp"] = now.UtcDateTime.ToString("o"),
        };
        if (Uri.TryCreate(n.Url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            embed["url"] = uri.AbsoluteUri;
        if (n.Fields is { Count: > 0 })
        {
            var fields = new JsonArray();
            foreach (var f in n.Fields.Take(25))
                fields.Add(new JsonObject
                {
                    ["name"] = Truncate(f.Name, 256),
                    ["value"] = Truncate(string.IsNullOrWhiteSpace(f.Value) ? "-" : f.Value, 1024),
                    ["inline"] = true,
                });
            embed["fields"] = fields;
        }
        return new JsonObject
        {
            ["username"] = "FreelanceFlow",
            ["embeds"] = new JsonArray(embed),
        };
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
