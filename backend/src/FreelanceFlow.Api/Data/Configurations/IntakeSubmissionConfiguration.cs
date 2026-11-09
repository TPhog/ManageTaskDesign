using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Intake;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.RequestTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class IntakeSubmissionConfiguration : IEntityTypeConfiguration<IntakeSubmission>
{
    public void Configure(EntityTypeBuilder<IntakeSubmission> b)
    {
        b.ToTable("intake_submissions", t =>
        {
            t.HasCheckConstraint("ck_intake_contact_name", "char_length(contact_name) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_intake_contact_phone", "char_length(contact_phone) BETWEEN 1 AND 20");
            t.HasCheckConstraint("ck_intake_contact_email", "contact_email IS NULL OR char_length(contact_email) <= 200");
            t.HasCheckConstraint("ck_intake_answers", "jsonb_typeof(answers) = 'array'");
            t.HasCheckConstraint("ck_intake_note", "char_length(note) <= 2000");
            t.HasCheckConstraint("ck_intake_body", "char_length(body) BETWEEN 1 AND 10000");
            t.HasCheckConstraint("ck_intake_status", "status IN ('NEW','CONVERTED','REJECTED')");
            t.HasCheckConstraint("ck_intake_reject_reason", "reject_reason IS NULL OR char_length(reject_reason) <= 500");
            t.HasCheckConstraint("ck_intake_converted",
                "(status = 'CONVERTED') = (converted_client_id IS NOT NULL AND converted_project_id IS NOT NULL AND converted_at IS NOT NULL)");
            t.HasCheckConstraint("ck_intake_rejected", "(status = 'REJECTED') = (rejected_at IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TemplateId).HasColumnName("template_id");
        b.Property(x => x.TemplateName).HasColumnName("template_name");
        b.Property(x => x.ContactName).HasColumnName("contact_name").IsRequired();
        b.Property(x => x.ContactPhone).HasColumnName("contact_phone").IsRequired();
        b.Property(x => x.ContactEmail).HasColumnName("contact_email");
        b.Property(x => x.AnswersJson).HasColumnName("answers").HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb").IsRequired();
        b.Property(x => x.Note).HasColumnName("note").HasDefaultValue("").IsRequired();
        b.Property(x => x.Body).HasColumnName("body").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("NEW").IsRequired();
        b.Property(x => x.RejectReason).HasColumnName("reject_reason");
        b.Property(x => x.ConvertedClientId).HasColumnName("converted_client_id");
        b.Property(x => x.ConvertedProjectId).HasColumnName("converted_project_id");
        b.Property(x => x.ConvertedAt).HasColumnName("converted_at");
        b.Property(x => x.RejectedAt).HasColumnName("rejected_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => new { x.Status, x.CreatedAt }).IsDescending(false, true).HasDatabaseName("ix_intake_status");
        b.HasOne<RequestTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Client>().WithMany().HasForeignKey(x => x.ConvertedClientId);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ConvertedProjectId);

        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
