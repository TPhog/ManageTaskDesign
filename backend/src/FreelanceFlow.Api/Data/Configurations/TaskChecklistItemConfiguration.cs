using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class TaskChecklistItemConfiguration : IEntityTypeConfiguration<TaskChecklistItem>
{
    public void Configure(EntityTypeBuilder<TaskChecklistItem> b)
    {
        b.ToTable("task_checklist_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TaskId).HasColumnName("task_id");
        b.Property(x => x.Text).HasColumnName("text").IsRequired();
        b.Property(x => x.Done).HasColumnName("done").HasDefaultValue(false);
        b.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
    }
}
