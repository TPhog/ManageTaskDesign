using FreelanceFlow.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class QuotationItemConfiguration : IEntityTypeConfiguration<QuotationItem>
{
    public void Configure(EntityTypeBuilder<QuotationItem> b)
    {
        b.ToTable("quotation_items", t =>
        {
            t.HasCheckConstraint("ck_qitems_description", "length(description) BETWEEN 1 AND 500");
            t.HasCheckConstraint("ck_qitems_quantity", "quantity >= 1");
            t.HasCheckConstraint("ck_qitems_unit_price", "unit_price >= 0");
            t.HasCheckConstraint("ck_qitems_amount", "amount = quantity * unit_price");
            t.HasCheckConstraint("ck_qitems_source_type",
                "source_type IN ('MANUAL','SERVICE','DELIVERABLE','REVISION')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.QuotationId).HasColumnName("quotation_id");
        b.Property(x => x.Description).HasColumnName("description").IsRequired();
        b.Property(x => x.Details).HasColumnName("details").HasDefaultValue("");
        b.Property(x => x.Quantity).HasColumnName("quantity");
        b.Property(x => x.UnitPrice).HasColumnName("unit_price");
        b.Property(x => x.Amount).HasColumnName("amount");
        b.Property(x => x.SourceType).HasColumnName("source_type").HasDefaultValue("MANUAL");
        b.Property(x => x.SourceId).HasColumnName("source_id");
        b.Property(x => x.CopiedFromItemId).HasColumnName("copied_from_item_id");
        b.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);

        b.HasIndex(x => new { x.QuotationId, x.SortOrder }).HasDatabaseName("ix_quotation_items_quotation");
        b.HasOne<QuotationItem>().WithMany().HasForeignKey(x => x.CopiedFromItemId);
    }
}
