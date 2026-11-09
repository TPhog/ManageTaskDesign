using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class DeliverableConfiguration : IEntityTypeConfiguration<Deliverable>
{
    public void Configure(EntityTypeBuilder<Deliverable> b)
    {
        b.ToTable("deliverables", t =>
        {
            t.HasCheckConstraint("ck_deliverables_quantity", "quantity >= 1");
            t.HasCheckConstraint("ck_deliverables_rounds", "included_rounds BETWEEN 0 AND 50");
            t.HasCheckConstraint("ck_deliverables_extra_price", "extra_round_price >= 0");
            t.HasCheckConstraint("ck_deliverables_status",
                "status IN ('NOT_STARTED','IN_PROGRESS','IN_REVIEW','APPROVED','DELIVERED','CANCELLED')");
            t.HasCheckConstraint("ck_deliverables_client_decision", "client_decision IN ('APPROVED')");
            t.HasCheckConstraint("ck_deliverables_links",
                "jsonb_typeof(links) = 'array' AND jsonb_array_length(links) <= 10");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.ServiceId).HasColumnName("service_id");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Quantity).HasColumnName("quantity").HasDefaultValue(1);
        b.Property(x => x.IncludedRounds).HasColumnName("included_rounds");
        b.Property(x => x.ExtraRoundPrice).HasColumnName("extra_round_price");
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("NOT_STARTED");
        b.Property(x => x.DueDate).HasColumnName("due_date");
        b.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
        b.Property(x => x.ClientDecision).HasColumnName("client_decision").HasMaxLength(20);
        b.Property(x => x.ClientDecisionAt).HasColumnName("client_decision_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.LinksJson).HasColumnName("links").HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb").IsRequired();

        b.HasIndex(x => new { x.ProjectId, x.SortOrder }).HasDatabaseName("ix_deliverables_project");

        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
        b.HasOne<Service>().WithMany().HasForeignKey(x => x.ServiceId);
    }
}
