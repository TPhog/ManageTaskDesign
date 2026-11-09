using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> b)
    {
        b.ToTable("tasks", t =>
        {
            t.HasCheckConstraint("ck_tasks_status",
                "status IN ('TODO','IN_PROGRESS','REVIEW','DONE','BLOCKED')");
            t.HasCheckConstraint("ck_tasks_priority",
                "priority IN ('LOW','MEDIUM','HIGH','URGENT')");
            t.HasCheckConstraint("ck_tasks_recurrence_kind",
                "recurrence_kind IS NULL OR recurrence_kind IN ('DAILY','WEEKLY','MONTHLY')");
            t.HasCheckConstraint("ck_tasks_blocked_status",
                "(blocked_reason IS NULL AND blocked_by_task_id IS NULL) OR status = 'BLOCKED'");
            t.HasCheckConstraint("ck_tasks_links",
                "jsonb_typeof(links) = 'array' AND jsonb_array_length(links) <= 10");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.DeliverableId).HasColumnName("deliverable_id");
        b.Property(x => x.RevisionItemId).HasColumnName("revision_item_id");
        b.Property(x => x.Title).HasColumnName("title").IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasDefaultValue("");
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("TODO");
        b.Property(x => x.Priority).HasColumnName("priority").HasDefaultValue("MEDIUM");
        b.Property(x => x.StartDate).HasColumnName("start_date");
        b.Property(x => x.DueDate).HasColumnName("due_date");
        b.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0.0);
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        // P15
        b.Property(x => x.LinksJson).HasColumnName("links").HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb").IsRequired();
        b.Property(x => x.RecurrenceKind).HasColumnName("recurrence_kind").HasMaxLength(10);
        b.Property(x => x.RecurrenceWeekdays).HasColumnName("recurrence_weekdays");
        b.Property(x => x.RecurrenceMonthDay).HasColumnName("recurrence_month_day");
        b.Property(x => x.RecurrenceUntil).HasColumnName("recurrence_until");
        b.Property(x => x.RecurrenceSpawned).HasColumnName("recurrence_spawned");
        b.Property(x => x.RecurrenceSourceId).HasColumnName("recurrence_source_id");
        b.Property(x => x.BlockedReason).HasColumnName("blocked_reason").HasMaxLength(ProjectTask.MaxBlockedReason);
        b.Property(x => x.BlockedByTaskId).HasColumnName("blocked_by_task_id");

        b.HasIndex(x => new { x.ProjectId, x.Status, x.SortOrder }).HasDatabaseName("ix_tasks_project_status");
        b.HasIndex(x => x.BlockedByTaskId).HasDatabaseName("ix_tasks_blocked_by").HasFilter("blocked_by_task_id IS NOT NULL");
        b.HasIndex(x => x.RecurrenceSourceId).HasDatabaseName("ix_tasks_recurrence_source").HasFilter("recurrence_source_id IS NOT NULL");

        b.HasMany(x => x.ChecklistItems).WithOne(x => x.Task).HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
        b.HasOne<Deliverable>().WithMany().HasForeignKey(x => x.DeliverableId);
        b.HasOne<RevisionItem>().WithMany().HasForeignKey(x => x.RevisionItemId);
        // P15: deleting the blocker / the previous occurrence keeps this task and only drops the link
        b.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.BlockedByTaskId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.RecurrenceSourceId).OnDelete(DeleteBehavior.SetNull);
    }
}
