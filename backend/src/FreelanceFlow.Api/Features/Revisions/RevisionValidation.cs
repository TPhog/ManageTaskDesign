using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Tasks;

namespace FreelanceFlow.Api.Features.Revisions;

/// <summary>400 field errors (docs/05 §1.2); the domain re-checks the same rules.</summary>
internal static class RevisionValidation
{
    public const int MaxRounds = 20;
    public const int MaxItemsPerRound = 50;
    public const int MaxNotes = 5000;
    public const int MaxProgressMessage = 1000;

    public static Dictionary<string, string[]> Rounds(RoundBody[]? rounds)
    {
        var errors = new Dictionary<string, string[]>();
        if (rounds is null || rounds.Length == 0)
        {
            errors["rounds"] = ["Cần ít nhất 1 vòng sửa"];
            return errors;
        }
        if (rounds.Length > MaxRounds) errors["rounds"] = [$"Tối đa {MaxRounds} vòng mỗi lần"];
        for (var r = 0; r < rounds.Length; r++) Round(rounds[r], $"rounds[{r}].", errors);
        return errors;
    }

    public static Dictionary<string, string[]> Round(RoundBody body)
    {
        var errors = new Dictionary<string, string[]>();
        Round(body, "", errors);
        return errors;
    }

    public static Dictionary<string, string[]> Item(string? description, string? type, string? billing, bool billingRequired)
    {
        var errors = new Dictionary<string, string[]>();
        Item(description, type, billing, billingRequired, "", errors);
        return errors;
    }

    public static Dictionary<string, string[]> CreateRequest(CreateRequestBody body)
    {
        var errors = new Dictionary<string, string[]>();
        var length = body.Content?.Trim().Length ?? 0;
        if (length is < 1 or > ClientRequest.MaxContent)
            errors["content"] = [$"Nội dung dài 1–{ClientRequest.MaxContent} ký tự"];
        if (body.Source is null || !RequestSources.Designer.Contains(body.Source))
            errors["source"] = ["Chọn nguồn: Zalo, Email, Điện thoại hoặc Khác"];
        return errors;
    }

    public static Dictionary<string, string[]> Decline(DeclineBody body)
    {
        var length = body.Reason?.Trim().Length ?? 0;
        return length is < 1 or > ClientRequest.MaxDeclineReason
            ? new() { ["reason"] = [$"Bắt buộc lý do từ chối (tối đa {ClientRequest.MaxDeclineReason} ký tự)"] }
            : new();
    }

    public static Dictionary<string, string[]> WorkStatus(WorkStatusBody body) =>
        body.To is null || !RevisionWorkStatuses.All.Contains(body.To)
            ? new() { ["to"] = ["Trạng thái không hợp lệ"] }
            : new();

    public static Dictionary<string, string[]> ItemTask(CreateItemTaskBody body)
    {
        var errors = new Dictionary<string, string[]>();
        if (body.Title is { } title && title.Trim().Length > 200) errors["title"] = ["Tối đa 200 ký tự"];
        if (body.Priority is { } priority && !ProjectTask.Priorities.Contains(priority))
            errors["priority"] = ["Mức ưu tiên không hợp lệ"];
        return errors;
    }

    public static Dictionary<string, string[]> ProgressUpdate(ProgressUpdateBody body)
    {
        var length = body.Message?.Trim().Length ?? 0;
        return length is < 1 or > MaxProgressMessage
            ? new() { ["message"] = [$"Nội dung cập nhật dài 1–{MaxProgressMessage} ký tự"] }
            : new();
    }

    private static void Round(RoundBody body, string prefix, Dictionary<string, string[]> errors)
    {
        if (body.Items is null || body.Items.Length == 0)
        {
            errors[prefix + "items"] = ["Cần ít nhất 1 mục sửa"];
            return;
        }
        if (body.Items.Length > MaxItemsPerRound) errors[prefix + "items"] = [$"Tối đa {MaxItemsPerRound} mục mỗi vòng"];
        for (var i = 0; i < body.Items.Length; i++)
        {
            var item = body.Items[i];
            Item(item.Description, item.Type, item.Billing, false, $"{prefix}items[{i}].", errors);
        }
    }

    private static void Item(string? description, string? type, string? billing, bool billingRequired,
        string prefix, Dictionary<string, string[]> errors)
    {
        var length = description?.Trim().Length ?? 0;
        if (length is < 1 or > RevisionItem.MaxDescription)
            errors[prefix + "description"] = [$"Mô tả dài 1–{RevisionItem.MaxDescription} ký tự"];
        if (type is null || !RevisionItemTypes.All.Contains(type))
            errors[prefix + "type"] = ["Loại yêu cầu sửa không hợp lệ"];
        if (billing is null ? billingRequired : !RevisionBillings.All.Contains(billing))
            errors[prefix + "billing"] = ["Cách tính phí không hợp lệ"];
    }
}
