using FreelanceFlow.Api.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> b)
    {
        b.ToTable("banks");
        b.HasKey(x => x.Bin);
        b.Property(x => x.Bin).HasColumnName("bin").HasMaxLength(6);
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.ShortName).HasColumnName("short_name").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true);
    }
}
