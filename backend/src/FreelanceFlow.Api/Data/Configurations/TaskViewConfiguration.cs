using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class TaskViewConfiguration : IEntityTypeConfiguration<TaskView>
{
    public void Configure(EntityTypeBuilder<TaskView> b)
    {
        b.ToTable("task_views", t =>
        {
            t.HasCheckConstraint("ck_task_views_name", "char_length(name) BETWEEN 1 AND 60");
            t.HasCheckConstraint("ck_task_views_config", "jsonb_typeof(config) = 'object'");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.ConfigJson).HasColumnName("config").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        // Unique on lower(name): EF cannot model the expression, the index is created by hand in 20261114090000_AddTaskLarkUpgrades
    }
}

public class TaskViewStateConfiguration : IEntityTypeConfiguration<TaskViewState>
{
    public void Configure(EntityTypeBuilder<TaskViewState> b)
    {
        b.ToTable("task_view_state", t =>
        {
            t.HasCheckConstraint("ck_task_view_state_singleton", "id = 1");
            t.HasCheckConstraint("ck_task_view_state_config", "jsonb_typeof(config) = 'object'");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("smallint").ValueGeneratedNever();
        b.Property(x => x.ConfigJson).HasColumnName("config").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
