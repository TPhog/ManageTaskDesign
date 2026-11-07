using FreelanceFlow.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class QuotationSequenceConfiguration : IEntityTypeConfiguration<QuotationSequence>
{
    public void Configure(EntityTypeBuilder<QuotationSequence> b)
    {
        b.ToTable("quotation_sequences");
        b.HasKey(x => x.Year);
        b.Property(x => x.Year).HasColumnName("year").ValueGeneratedNever();
        b.Property(x => x.LastSeq).HasColumnName("last_seq").HasDefaultValue(0);
    }
}
