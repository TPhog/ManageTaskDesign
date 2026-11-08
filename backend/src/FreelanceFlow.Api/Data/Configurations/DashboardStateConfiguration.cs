using FreelanceFlow.Api.Domain.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreelanceFlow.Api.Data.Configurations;

public class DashboardStateConfiguration : IEntityTypeConfiguration<DashboardState>
{
    public void Configure(EntityTypeBuilder<DashboardState> b)
    {
        b.ToTable("dashboard_state", t => t.HasCheckConstraint("ck_dashboard_state_singleton", "id = 1"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("smallint").ValueGeneratedNever();
        b.Property(x => x.DigestLastRunDate).HasColumnName("digest_last_run_date");
        b.Property(x => x.SetupDismissedAt).HasColumnName("setup_dismissed_at");
    }
}
