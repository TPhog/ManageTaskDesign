using FreelanceFlow.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class DesignerAccountConfiguration : IEntityTypeConfiguration<DesignerAccount>
{
    public void Configure(EntityTypeBuilder<DesignerAccount> b)
    {
        b.ToTable("designer_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Username).HasColumnName("username").IsRequired();
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
        b.Property(x => x.SecurityStamp).HasColumnName("security_stamp").IsRequired();
        b.Property(x => x.FailedLogins).HasColumnName("failed_logins").HasDefaultValue(0);
        b.Property(x => x.LockoutUntil).HasColumnName("lockout_until");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
