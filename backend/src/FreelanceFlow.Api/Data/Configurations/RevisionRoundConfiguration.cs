using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class RevisionRoundConfiguration : IEntityTypeConfiguration<RevisionRound>
{
    public void Configure(EntityTypeBuilder<RevisionRound> b)
    {
        b.ToTable("revision_rounds", t =>
        {
            t.HasCheckConstraint("ck_rounds_no", "round_no >= 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.DeliverableId).HasColumnName("deliverable_id");
        b.Property(x => x.RoundNo).HasColumnName("round_no");
        b.Property(x => x.SourceRequestId).HasColumnName("source_request_id");
        b.Property(x => x.ReceivedAt).HasColumnName("received_at");
        b.Property(x => x.Notes).HasColumnName("notes").HasDefaultValue("");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
        b.HasOne<Deliverable>().WithMany().HasForeignKey(x => x.DeliverableId);
        b.HasOne<ClientRequest>().WithMany().HasForeignKey(x => x.SourceRequestId);
    }
}
