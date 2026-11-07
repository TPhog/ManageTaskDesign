using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class ClientRequestConfiguration : IEntityTypeConfiguration<ClientRequest>
{
    public void Configure(EntityTypeBuilder<ClientRequest> b)
    {
        b.ToTable("client_requests", t =>
        {
            t.HasCheckConstraint("ck_requests_content", "length(content) BETWEEN 1 AND 5000");
            t.HasCheckConstraint("ck_requests_source", "source IN ('PORTAL','ZALO','EMAIL','PHONE','OTHER')");
            t.HasCheckConstraint("ck_requests_created_by", "created_by IN ('CLIENT','DESIGNER')");
            t.HasCheckConstraint("ck_requests_status", "status IN ('NEW','TRIAGED','DECLINED')");
            t.HasCheckConstraint("ck_requests_decline", "status <> 'DECLINED' OR decline_reason IS NOT NULL");
            t.HasCheckConstraint("ck_requests_portal", "created_by <> 'CLIENT' OR source = 'PORTAL'");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.DeliverableId).HasColumnName("deliverable_id");
        b.Property(x => x.Content).HasColumnName("content").IsRequired();
        b.Property(x => x.Source).HasColumnName("source").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("NEW");
        b.Property(x => x.DeclineReason).HasColumnName("decline_reason");
        b.Property(x => x.TriagedAt).HasColumnName("triaged_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => new { x.Status, x.CreatedAt }).IsDescending(false, true).HasDatabaseName("ix_requests_status");
        b.HasIndex(x => x.ProjectId).HasDatabaseName("ix_requests_project");

        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
        b.HasOne<Deliverable>().WithMany().HasForeignKey(x => x.DeliverableId);

        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
