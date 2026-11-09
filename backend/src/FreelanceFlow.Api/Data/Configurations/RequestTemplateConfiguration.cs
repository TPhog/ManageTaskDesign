using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.RequestTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class RequestTemplateConfiguration : IEntityTypeConfiguration<RequestTemplate>
{
    public void Configure(EntityTypeBuilder<RequestTemplate> b)
    {
        b.ToTable("request_templates", t =>
        {
            t.HasCheckConstraint("ck_request_templates_name", "char_length(name) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_request_templates_description", "char_length(description) <= 500");
            t.HasCheckConstraint("ck_request_templates_design_type",
                "design_type IN ('LOGO','BANNER','POSTER','BRANDING','UI_UX','SOCIAL_MEDIA','PACKAGING','PRINT','OTHER')");
            t.HasCheckConstraint("ck_request_templates_fields",
                "jsonb_typeof(fields) = 'array' AND jsonb_array_length(fields) <= 30");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasDefaultValue("").IsRequired();
        b.Property(x => x.DesignType).HasColumnName("design_type").IsRequired();
        b.Property(x => x.ServiceId).HasColumnName("service_id");
        b.Property(x => x.FieldsJson).HasColumnName("fields").HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb").IsRequired();
        // no HasDefaultValue(true): EF would skip a CLR false (sentinel) and the row would come back active
        b.Property(x => x.Active).HasColumnName("active");
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => new { x.SortOrder, x.CreatedAt }).HasDatabaseName("ix_request_templates_order");
        b.HasOne<Service>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.SetNull);

        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
