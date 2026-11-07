using FreelanceFlow.Api.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("clients", t =>
        {
            t.HasCheckConstraint("ck_clients_status", "status IN ('ACTIVE','INACTIVE')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Company).HasColumnName("company");
        b.Property(x => x.Phone).HasColumnName("phone");
        b.Property(x => x.Email).HasColumnName("email");
        b.Property(x => x.ContactChannel).HasColumnName("contact_channel");
        b.Property(x => x.Notes).HasColumnName("notes").HasDefaultValue("");
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("ACTIVE");
        b.Property(x => x.PortalEnabled).HasColumnName("portal_enabled").HasDefaultValue(false);
        b.Property(x => x.PortalTokenHash).HasColumnName("portal_token_hash").HasColumnType("bytea");
        b.Property(x => x.PortalTokenVersion).HasColumnName("portal_token_version").HasDefaultValue(0);
        b.Property(x => x.PortalTokenCreatedAt).HasColumnName("portal_token_created_at");
        b.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
