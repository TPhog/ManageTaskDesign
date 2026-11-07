using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Activity;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Infrastructure;

public class ActivityWriter(AppDbContext db, IClock clock)
{
    public void Write(
        string entityType,
        Guid entityId,
        string action,
        Guid? clientId = null,
        Guid? projectId = null,
        string actorType = "DESIGNER",
        object? data = null,
        bool visibleToClient = false,
        string? clientMessage = null)
    {
        db.ActivityLogs.Add(new ActivityLog
        {
            OccurredAt = clock.UtcNow,
            ActorType = actorType,
            ClientId = clientId,
            ProjectId = projectId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Data = data == null ? "{}" : JsonSerializer.Serialize(data, _jsonOptions),
            VisibleToClient = visibleToClient,
            ClientMessage = clientMessage,
        });
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
}
