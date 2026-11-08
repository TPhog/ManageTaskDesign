using FreelanceFlow.Api.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class StorageAlertConfiguration : IEntityTypeConfiguration<StorageAlert>
{
    public void Configure(EntityTypeBuilder<StorageAlert> b)
    {
        b.ToTable("storage_alerts", t =>
            t.HasCheckConstraint("ck_storage_alerts_threshold", "threshold IN (50,70,85,90,95)"));
        b.HasKey(x => x.Threshold);
        b.Property(x => x.Threshold).HasColumnName("threshold").ValueGeneratedNever();
        b.Property(x => x.NotifiedAt).HasColumnName("notified_at");
        b.Property(x => x.ClearedAt).HasColumnName("cleared_at");
    }
}
