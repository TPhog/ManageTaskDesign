using FreelanceFlow.Api.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Settings = FreelanceFlow.Api.Domain.Settings.Settings;

namespace FreelanceFlow.Api.Data.Configurations;

public class SettingsConfiguration : IEntityTypeConfiguration<Settings>
{
    public void Configure(EntityTypeBuilder<Settings> b)
    {
        b.ToTable("settings", t =>
        {
            t.HasCheckConstraint("ck_settings_singleton", "id = 1");
            t.HasCheckConstraint("ck_settings_account_no",
                "bank_account_no ~ '^[0-9A-Za-z]{4,19}$' OR bank_account_no IS NULL");
            t.HasCheckConstraint("ck_settings_prefix", "quotation_prefix ~ '^[A-Z]{1,5}$'");
            t.HasCheckConstraint("ck_settings_validity", "default_validity_days BETWEEN 1 AND 365");
            t.HasCheckConstraint("ck_settings_deposit", "default_deposit_percent BETWEEN 0 AND 100");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("smallint").ValueGeneratedNever();
        b.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        b.Property(x => x.ContactEmail).HasColumnName("contact_email");
        b.Property(x => x.ContactPhone).HasColumnName("contact_phone");
        b.Property(x => x.LogoFileId).HasColumnName("logo_file_id");
        b.Property(x => x.BankBin).HasColumnName("bank_bin").HasMaxLength(6);
        b.Property(x => x.BankAccountNo).HasColumnName("bank_account_no");
        b.Property(x => x.BankAccountName).HasColumnName("bank_account_name");
        b.Property(x => x.QuotationPrefix).HasColumnName("quotation_prefix").HasDefaultValue("BG");
        b.Property(x => x.DefaultValidityDays).HasColumnName("default_validity_days").HasDefaultValue(7);
        b.Property(x => x.DefaultDepositPercent).HasColumnName("default_deposit_percent").HasDefaultValue(50);
        b.Property(x => x.DefaultTerms).HasColumnName("default_terms").HasDefaultValue("");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasOne(x => x.Bank).WithMany().HasForeignKey(x => x.BankBin).HasPrincipalKey(bk => bk.Bin);
        b.HasOne<ProjectFile>().WithMany().HasForeignKey(x => x.LogoFileId);

        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
