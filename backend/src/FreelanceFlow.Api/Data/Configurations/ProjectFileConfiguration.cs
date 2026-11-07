using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class ProjectFileConfiguration : IEntityTypeConfiguration<ProjectFile>
{
    public void Configure(EntityTypeBuilder<ProjectFile> b)
    {
        b.ToTable("files", t =>
        {
            t.HasCheckConstraint("ck_files_kind",
                "kind IN ('REFERENCE','PREVIEW','FINAL','EVIDENCE','BRIEF','SYSTEM')");
            t.HasCheckConstraint("ck_files_size", "size_bytes > 0");
            t.HasCheckConstraint("ck_files_uploaded_by", "uploaded_by IN ('DESIGNER','CLIENT')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.DeliverableId).HasColumnName("deliverable_id");
        b.Property(x => x.RequestId).HasColumnName("request_id");
        b.Property(x => x.Kind).HasColumnName("kind").IsRequired();
        b.Property(x => x.OriginalName).HasColumnName("original_name").IsRequired();
        b.Property(x => x.ContentType).HasColumnName("content_type").IsRequired();
        b.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        b.Property(x => x.StorageKey).HasColumnName("storage_key").IsRequired();
        b.HasIndex(x => x.StorageKey).IsUnique();
        b.Property(x => x.Sha256).HasColumnName("sha256").HasColumnType("bytea").IsRequired();
        b.Property(x => x.VisibleToClient).HasColumnName("visible_to_client").HasDefaultValue(false);
        b.Property(x => x.UploadedBy).HasColumnName("uploaded_by").IsRequired();
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");

        b.HasIndex(x => new { x.ProjectId, x.CreatedAt }).IsDescending(false, true)
            .HasFilter("deleted_at IS NULL").HasDatabaseName("ix_files_project");
        b.HasIndex(x => x.RequestId).HasDatabaseName("ix_files_request");

        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
        b.HasOne<Deliverable>().WithMany().HasForeignKey(x => x.DeliverableId);
        b.HasOne<ClientRequest>().WithMany().HasForeignKey(x => x.RequestId);
    }
}
