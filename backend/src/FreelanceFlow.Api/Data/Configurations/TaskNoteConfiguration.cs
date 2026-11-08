using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class TaskNoteConfiguration : IEntityTypeConfiguration<TaskNote>
{
    public void Configure(EntityTypeBuilder<TaskNote> b)
    {
        b.ToTable("task_notes", t =>
        {
            t.HasCheckConstraint("ck_task_notes_body", "char_length(body) BETWEEN 1 AND 5000");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TaskId).HasColumnName("task_id");
        b.Property(x => x.Body).HasColumnName("body").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => new { x.TaskId, x.CreatedAt }).IsDescending(false, true)
            .HasDatabaseName("ix_task_notes_task");

        b.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
