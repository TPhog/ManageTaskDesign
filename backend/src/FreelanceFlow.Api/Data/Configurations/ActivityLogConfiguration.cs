using FreelanceFlow.Api.Domain.Activity;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> b)
    {
        b.ToTable("activity_logs", t =>
        {
            t.HasCheckConstraint("ck_activity_actor_type", "actor_type IN ('DESIGNER','CLIENT','SYSTEM')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasDefaultValueSql("now()");
        b.Property(x => x.ActorType).HasColumnName("actor_type").IsRequired();
        b.Property(x => x.ClientId).HasColumnName("client_id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.EntityType).HasColumnName("entity_type").IsRequired();
        b.Property(x => x.EntityId).HasColumnName("entity_id");
        b.Property(x => x.Action).HasColumnName("action").IsRequired();
        b.Property(x => x.Data).HasColumnName("data").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
        b.Property(x => x.VisibleToClient).HasColumnName("visible_to_client").HasDefaultValue(false);
        b.Property(x => x.ClientMessage).HasColumnName("client_message");

        b.HasIndex(x => new { x.ProjectId, x.OccurredAt }).IsDescending(false, true)
            .HasDatabaseName("ix_activity_project");
        b.HasIndex(x => new { x.ClientId, x.OccurredAt }).IsDescending(false, true)
            .HasDatabaseName("ix_activity_client");
        b.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("ix_activity_entity");

        b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId);
    }
}
