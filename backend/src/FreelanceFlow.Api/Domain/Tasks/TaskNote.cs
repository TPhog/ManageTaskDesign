using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Tasks;

/// <summary>Plan 06b F5: timestamped designer note on a task (never shown on the portal). Deleted with its task (FK cascade).</summary>
public class TaskNote : IAuditable
{
    public const int MaxBody = 5000;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TaskId { get; set; }
    public string Body { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static TaskNote Create(Guid taskId, string body)
    {
        var note = new TaskNote { TaskId = taskId };
        note.Edit(body);
        return note;
    }

    public void Edit(string body)
    {
        var text = (body ?? "").Trim();
        if (text.Length is < 1 or > MaxBody)
            throw new DomainException("VALIDATION_FAILED", $"Ghi chú dài 1–{MaxBody} ký tự");
        Body = text;
    }
}
