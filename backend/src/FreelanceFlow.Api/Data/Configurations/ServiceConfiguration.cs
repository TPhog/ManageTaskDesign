using FreelanceFlow.Api.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.ToTable("services", t =>
        {
            t.HasCheckConstraint("ck_services_base_price", "base_price >= 0");
            t.HasCheckConstraint("ck_services_rounds", "default_included_rounds BETWEEN 0 AND 50");
            t.HasCheckConstraint("ck_services_extra_price", "extra_round_price >= 0");
            t.HasCheckConstraint("ck_services_task_template",
                "jsonb_typeof(task_template) = 'array' AND jsonb_array_length(task_template) <= 20");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasDefaultValue("");
        b.Property(x => x.BasePrice).HasColumnName("base_price");
        b.Property(x => x.DefaultIncludedRounds).HasColumnName("default_included_rounds");
        b.Property(x => x.ExtraRoundPrice).HasColumnName("extra_round_price");
        b.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true);
        b.Property(x => x.TaskTemplateJson).HasColumnName("task_template").HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
