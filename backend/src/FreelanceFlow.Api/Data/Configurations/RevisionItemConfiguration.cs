using FreelanceFlow.Api.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class RevisionItemConfiguration : IEntityTypeConfiguration<RevisionItem>
{
    public void Configure(EntityTypeBuilder<RevisionItem> b)
    {
        b.ToTable("revision_items", t =>
        {
            t.HasCheckConstraint("ck_items_description", "length(description) BETWEEN 1 AND 2000");
            t.HasCheckConstraint("ck_items_type", "type IN ('MINOR','MAJOR','SCOPE_CHANGE','NEW_DELIVERABLE')");
            t.HasCheckConstraint("ck_items_billing", "billing IN ('FREE_FIX','IN_QUOTA','CHARGEABLE')");
            t.HasCheckConstraint("ck_items_work_status", "work_status IN ('TODO','IN_PROGRESS','DONE','CANCELLED')");
            t.HasCheckConstraint("ck_items_quotation", "billing = 'CHARGEABLE' OR quotation_item_id IS NULL");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.RoundId).HasColumnName("round_id");
        b.Property(x => x.Description).HasColumnName("description").IsRequired();
        b.Property(x => x.Type).HasColumnName("type").IsRequired();
        b.Property(x => x.Billing).HasColumnName("billing").IsRequired();
        b.Property(x => x.WorkStatus).HasColumnName("work_status").HasDefaultValue("TODO");
        b.Property(x => x.QuotationItemId).HasColumnName("quotation_item_id");
        b.Property(x => x.ScopeOverrideReason).HasColumnName("scope_override_reason");
        b.Property(x => x.InternalNotes).HasColumnName("internal_notes").HasDefaultValue("");
        b.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => new { x.RoundId, x.SortOrder }).HasDatabaseName("ix_items_round");
        b.HasIndex(x => x.QuotationItemId).HasDatabaseName("ix_items_quotation_item");

        b.HasOne<RevisionRound>().WithMany().HasForeignKey(x => x.RoundId);
        // fk_items_quotation_item added via raw SQL in migration (circular dependency)

        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
