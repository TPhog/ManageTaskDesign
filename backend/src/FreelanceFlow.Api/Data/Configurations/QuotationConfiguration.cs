using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> b)
    {
        b.ToTable("quotations", t =>
        {
            t.HasCheckConstraint("ck_quotations_kind", "kind IN ('INITIAL','ADDITIONAL')");
            t.HasCheckConstraint("ck_quotations_version", "version >= 1");
            t.HasCheckConstraint("ck_quotations_status",
                "status IN ('DRAFT','SENT','ACCEPTED','REJECTED','SUPERSEDED')");
            t.HasCheckConstraint("ck_quotations_expiry", "expiry_date IS NULL OR expiry_date >= issue_date");
            t.HasCheckConstraint("ck_quotations_validity", "validity_days BETWEEN 1 AND 365");
            t.HasCheckConstraint("ck_quotations_subtotal", "subtotal >= 0");
            t.HasCheckConstraint("ck_quotations_discount",
                "discount_amount >= 0 AND discount_amount <= subtotal");
            t.HasCheckConstraint("ck_quotations_vat_percent", "vat_percent BETWEEN 0 AND 20");
            t.HasCheckConstraint("ck_quotations_vat_amount", "vat_amount >= 0");
            t.HasCheckConstraint("ck_quotations_total", "total >= 0");
            t.HasCheckConstraint("ck_quotations_deposit_percent", "deposit_percent BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_quotations_deposit_amount", "deposit_amount >= 0");
            t.HasCheckConstraint("ck_quotations_payment_status",
                "payment_status IN ('UNPAID','DEPOSIT_PAID','PAID')");
            t.HasCheckConstraint("ck_quotations_accepted",
                "status <> 'ACCEPTED' OR accepted_at IS NOT NULL");
            t.HasCheckConstraint("ck_quotations_rejected",
                "status <> 'REJECTED' OR rejected_reason IS NOT NULL");
            t.HasCheckConstraint("ck_quotations_payment_accepted",
                "payment_status = 'UNPAID' OR status = 'ACCEPTED'");
            t.HasCheckConstraint("ck_quotations_deposit_paid",
                "payment_status <> 'DEPOSIT_PAID' OR deposit_percent > 0");
            t.HasCheckConstraint("ck_quotations_payment_notice_purpose",
                "client_payment_notice_purpose IS NULL OR client_payment_notice_purpose IN ('deposit','full','remaining')");
            t.HasCheckConstraint("ck_quotations_payment_notice_at",
                "(client_payment_notice_purpose IS NULL) = (client_payment_notice_at IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.Kind).HasColumnName("kind").IsRequired();
        b.Property(x => x.Number).HasColumnName("number");
        b.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1);
        b.Property(x => x.PreviousVersionId).HasColumnName("previous_version_id");
        b.Property(x => x.Title).HasColumnName("title").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("DRAFT");
        b.Property(x => x.IssueDate).HasColumnName("issue_date");
        b.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
        b.Property(x => x.ValidityDays).HasColumnName("validity_days");
        b.Property(x => x.Subtotal).HasColumnName("subtotal").HasDefaultValue(0L);
        b.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasDefaultValue(0L);
        b.Property(x => x.VatPercent).HasColumnName("vat_percent").HasDefaultValue(0);
        b.Property(x => x.VatAmount).HasColumnName("vat_amount").HasDefaultValue(0L);
        b.Property(x => x.Total).HasColumnName("total").HasDefaultValue(0L);
        b.Property(x => x.DepositPercent).HasColumnName("deposit_percent").HasDefaultValue(0);
        b.Property(x => x.DepositAmount).HasColumnName("deposit_amount").HasDefaultValue(0L);
        b.Property(x => x.Terms).HasColumnName("terms").HasDefaultValue("");
        b.Property(x => x.NotesToClient).HasColumnName("notes_to_client").HasDefaultValue("");
        b.Property(x => x.InternalNotes).HasColumnName("internal_notes").HasDefaultValue("");
        b.Property(x => x.Snapshot).HasColumnName("snapshot").HasColumnType("jsonb");
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.AcceptedAt).HasColumnName("accepted_at");
        b.Property(x => x.AcceptedNote).HasColumnName("accepted_note");
        b.Property(x => x.EvidenceFileId).HasColumnName("evidence_file_id");
        b.Property(x => x.RejectedAt).HasColumnName("rejected_at");
        b.Property(x => x.RejectedReason).HasColumnName("rejected_reason");
        b.Property(x => x.PaymentStatus).HasColumnName("payment_status").HasDefaultValue("UNPAID");
        b.Property(x => x.DepositPaidAt).HasColumnName("deposit_paid_at");
        b.Property(x => x.PaidAt).HasColumnName("paid_at");
        b.Property(x => x.ClientAcceptedAt).HasColumnName("client_accepted_at");
        b.Property(x => x.ClientPaymentNoticePurpose).HasColumnName("client_payment_notice_purpose").HasMaxLength(20);
        b.Property(x => x.ClientPaymentNoticeAt).HasColumnName("client_payment_notice_at");
        b.Property(x => x.ClientPaymentNoticeNote).HasColumnName("client_payment_notice_note").HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => new { x.Number, x.Version }).IsUnique().HasFilter("number IS NOT NULL")
            .HasDatabaseName("ux_quotations_number_version");
        b.HasIndex(x => new { x.ProjectId, x.CreatedAt }).IsDescending(false, true)
            .HasDatabaseName("ix_quotations_project");
        b.HasIndex(x => new { x.Status, x.ExpiryDate }).HasDatabaseName("ix_quotations_status");

        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.QuotationId);
        b.HasOne<Quotation>().WithMany().HasForeignKey(x => x.PreviousVersionId);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
        b.HasOne<ProjectFile>().WithMany().HasForeignKey(x => x.EvidenceFileId);

        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
