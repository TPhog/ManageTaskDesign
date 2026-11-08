using System.Globalization;
using System.Text;
using FreelanceFlow.Api.Domain.Dashboard;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Infrastructure.Notifications;

namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>P11 BR-94: the morning digest text. No webhook URL, portal token or bank account ever goes in it.</summary>
public static class DailyDigestMessage
{
    private const int MaxPerGroup = 5;
    private const int MaxLength = 3500;

    /// <summary>null when there is nothing to report. Lists are capped at 5 per group; Description ≤ 3500 chars.</summary>
    public static Notification? Build(DigestFacts f, DateOnly today, string? publicBaseUrl)
    {
        if (!f.HasContent) return null;
        var sb = new StringBuilder();
        Group(sb, "Quá hạn", f.OverdueCount, f.Overdue);
        Group(sb, "Hôm nay", f.TodayCount, f.Today);
        Group(sb, "Ngày mai", f.TomorrowCount, f.Tomorrow);
        if (f.ProjectDeadlines.Count > 0)
        {
            sb.AppendLine($"**Deadline dự án trong {DashboardRules.DigestDeadlineDays} ngày**");
            foreach (var d in f.ProjectDeadlines.Take(MaxPerGroup)) sb.AppendLine($"- {d.Name} · {Date(d.Date)}");
        }
        if (f.ExpiringQuotations.Count > 0)
        {
            sb.AppendLine($"**Báo giá hết hạn trong {DashboardRules.DigestDeadlineDays} ngày**");
            foreach (var q in f.ExpiringQuotations.Take(MaxPerGroup)) sb.AppendLine($"- {q.Number ?? "Báo giá nháp"} · hạn {Date(q.Expiry)}");
        }
        var description = sb.Length > MaxLength ? sb.ToString(0, MaxLength) + "…" : sb.ToString().TrimEnd();
        var urgent = f.OverdueCount > 0 || f.ExpiringQuotations.Count > 0;
        return new Notification(
            $"Nhắc việc ngày {Date(today)}",
            description,
            string.IsNullOrWhiteSpace(publicBaseUrl) ? null : publicBaseUrl.TrimEnd('/') + "/admin",
            urgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
            [
                new("Quá hạn", f.OverdueCount.ToString(CultureInfo.InvariantCulture)),
                new("Hôm nay", f.TodayCount.ToString(CultureInfo.InvariantCulture)),
                new("Ngày mai", f.TomorrowCount.ToString(CultureInfo.InvariantCulture)),
                new("Công nợ", RevisionQuota.Vnd(f.ReceivablesTotal)),
            ]);
    }

    private static void Group(StringBuilder sb, string title, int count, IReadOnlyList<DigestTask> tasks)
    {
        if (count == 0) return;
        sb.AppendLine($"**{title} ({count})**");
        foreach (var t in tasks.Take(MaxPerGroup)) sb.AppendLine($"- {t.Title} · {t.ProjectName} · {Date(t.DueDate)}");
        if (count > MaxPerGroup) sb.AppendLine($"- và {count - MaxPerGroup} việc khác");
    }

    private static string Date(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
