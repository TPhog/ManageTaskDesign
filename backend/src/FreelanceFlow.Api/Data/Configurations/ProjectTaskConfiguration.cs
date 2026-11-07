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

        b.HasIndex(x => new { x.ProjectId, x.Status, x.SortOrder }).HasDatabaseName("ix_tasks_project_status");

        b.HasMany(x => x.ChecklistItems).WithOne(x => x.Task).HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
        b.HasOne<Deliverable>().WithMany().HasForeignKey(x => x.DeliverableId);
        b.HasOne<RevisionItem>().WithMany().HasForeignKey(x => x.RevisionItemId);
    }
}
