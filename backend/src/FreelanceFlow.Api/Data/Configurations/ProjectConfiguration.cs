using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects", t =>
        {
            t.HasCheckConstraint("ck_projects_design_type",
                "design_type IN ('LOGO','BANNER','POSTER','BRANDING','UI_UX','SOCIAL_MEDIA','PACKAGING','PRINT','OTHER')");
            t.HasCheckConstraint("ck_projects_status",
                "status IN ('DRAFT','QUOTED','IN_PROGRESS','ON_HOLD','COMPLETED','CANCELLED')");
            t.HasCheckConstraint("ck_projects_dates",
                "due_date IS NULL OR start_date IS NULL OR due_date >= start_date");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ClientId).HasColumnName("client_id");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.DesignType).HasColumnName("design_type").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("DRAFT");
        b.Property(x => x.StatusReason).HasColumnName("status_reason");
        b.Property(x => x.StatusReasonPublic).HasColumnName("status_reason_public").HasDefaultValue(false);
        b.Property(x => x.StartDate).HasColumnName("start_date");
        b.Property(x => x.DueDate).HasColumnName("due_date");
        b.Property(x => x.InternalNotes).HasColumnName("internal_notes").HasDefaultValue("");
        b.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => x.ClientId).HasDatabaseName("ix_projects_client");
        b.HasIndex(x => new { x.Status, x.DueDate }).HasDatabaseName("ix_projects_status_due");

        b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId);

        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
