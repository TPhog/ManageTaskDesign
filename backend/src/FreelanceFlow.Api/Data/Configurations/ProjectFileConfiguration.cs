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
            t.HasCheckConstraint("ck_files_owner_type",
                "owner_type IS NULL OR owner_type IN ('TASK','PROGRESS_UPDATE','CLIENT_REQUEST','DELIVERABLE','INTAKE_SUBMISSION')");
            // Plan 13: only intake photos live without a project (until conversion), and never on a request
            t.HasCheckConstraint("ck_files_image_project",
                "owner_type IS NULL OR owner_type = 'INTAKE_SUBMISSION' OR project_id IS NOT NULL");
            t.HasCheckConstraint("ck_files_intake_request",
                "owner_type IS DISTINCT FROM 'INTAKE_SUBMISSION' OR request_id IS NULL");
            t.HasCheckConstraint("ck_files_owner", "(owner_type IS NULL) = (owner_id IS NULL)");
            // P10: only a client's own photo may be served from its original, and its only copy is never discarded
            t.HasCheckConstraint("ck_files_preview_alias",
                "NOT preview_is_original OR (uploaded_by = 'CLIENT' AND original_deleted_at IS NULL)");
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
        // Plan 08 - images
        b.Property(x => x.OwnerType).HasColumnName("owner_type");
        b.Property(x => x.OwnerId).HasColumnName("owner_id");
        b.Property(x => x.Width).HasColumnName("width");
        b.Property(x => x.Height).HasColumnName("height");
        b.Property(x => x.OriginalBytes).HasColumnName("original_bytes");
        b.Property(x => x.PreviewBytes).HasColumnName("preview_bytes");
        b.Property(x => x.ThumbBytes).HasColumnName("thumb_bytes");
        // P10
        b.Property(x => x.PreviewIsOriginal).HasColumnName("preview_is_original").HasDefaultValue(false);
        b.Property(x => x.OriginalDeletedAt).HasColumnName("original_deleted_at");
        b.HasIndex(x => new { x.OwnerType, x.OwnerId })
            .HasFilter("deleted_at IS NULL").HasDatabaseName("ix_files_owner");

        b.HasIndex(x => new { x.ProjectId, x.CreatedAt }).IsDescending(false, true)
            .HasFilter("deleted_at IS NULL").HasDatabaseName("ix_files_project");
        b.HasIndex(x => x.RequestId).HasDatabaseName("ix_files_request");
        b.HasIndex(x => new { x.ProjectId, x.Sha256 })
            .HasFilter("deleted_at IS NULL").HasDatabaseName("ix_files_project_sha256");   // P10 duplicate warning

        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
        b.HasOne<Deliverable>().WithMany().HasForeignKey(x => x.DeliverableId);
        b.HasOne<ClientRequest>().WithMany().HasForeignKey(x => x.RequestId);
    }
}
