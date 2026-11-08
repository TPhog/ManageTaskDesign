using System.Text;

namespace FreelanceFlow.Api.Domain.RequestTemplates;

/// <summary>BR-132: the plain-text body stored as client_requests.content / intake_submissions.body / brief v1.</summary>
public static class TemplateRenderer
{
    public static string Render(string? templateName, IReadOnlyList<TemplateAnswer> answers, string? note)
    {
        var sb = new StringBuilder();
        var title = templateName?.Trim();
        if (!string.IsNullOrEmpty(title)) sb.Append("Mẫu: ").Append(title).Append('\n');
        foreach (var a in answers)
        {
            var lines = a.Display.Replace("\r\n", "\n").Split('\n');
            sb.Append("- ").Append(a.Label).Append(": ").Append(lines[0]).Append('\n');
            foreach (var line in lines.Skip(1)) sb.Append("  ").Append(line).Append('\n');
        }
        var text = note?.Replace("\r\n", "\n").Trim();
        if (!string.IsNullOrEmpty(text))
        {
            if (sb.Length > 0) sb.Append("\nGhi chú thêm:\n");
            sb.Append(text).Append('\n');
        }
        return sb.ToString().TrimEnd();
    }
}
