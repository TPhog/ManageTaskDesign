using FreelanceFlow.Api.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class ProjectBriefConfiguration : IEntityTypeConfiguration<ProjectBrief>
{
    public void Configure(EntityTypeBuilder<ProjectBrief> b)
    {
        b.ToTable("project_briefs", t =>
        {
            t.HasCheckConstraint("ck_briefs_version", "version >= 1");
            t.HasCheckConstraint("ck_briefs_status", "status IN ('DRAFT','CONFIRMED')");
            t.HasCheckConstraint("ck_briefs_confirmed", "(status = 'CONFIRMED') = (confirmed_at IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.Version).HasColumnName("version");
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("DRAFT");
        b.Property(x => x.Content).HasColumnName("content").HasDefaultValue("");
        b.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique();

        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
    }
}
