using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Tasks;

/// <summary>P15 D7: the optional blocking task must be another task of the same project and must not close a cycle.</summary>
internal static class TaskBlocking
{
    private const int MaxChain = 20;

    public static async Task CheckAsync(AppDbContext db, ProjectTask task, Guid? byTaskId, Dictionary<string, string[]> errors)
    {
        if (byTaskId is not { } blockerId) return;
        if (blockerId == task.Id)
        {
            errors["blocked.byTaskId"] = ["Không thể tự chặn chính mình"];
            return;
        }

        var blocker = await db.Tasks.AsNoTracking()
            .Where(t => t.Id == blockerId)
            .Select(t => new { t.ProjectId, t.BlockedByTaskId })
            .FirstOrDefaultAsync();
        if (blocker == null || blocker.ProjectId != task.ProjectId)
        {
            errors["blocked.byTaskId"] = ["Công việc chặn phải thuộc cùng dự án"];
            return;
        }

        // Walk the blocker's own chain: reaching this task means a <- … <- a
        var next = blocker.BlockedByTaskId;
        for (var step = 0; next is { } current && step < MaxChain; step++)
        {
            if (current == task.Id)
            {
                errors["blocked.byTaskId"] = ["Việc chặn tạo vòng lặp"];
                return;
            }
            next = await db.Tasks.AsNoTracking().Where(t => t.Id == current).Select(t => t.BlockedByTaskId).FirstOrDefaultAsync();
        }
    }
}
