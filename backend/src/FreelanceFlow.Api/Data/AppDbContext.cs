using FreelanceFlow.Api.Domain.Activity;
using FreelanceFlow.Api.Domain.Auth;
using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Dashboard;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Settings = FreelanceFlow.Api.Domain.Settings.Settings;
using Bank = FreelanceFlow.Api.Domain.Settings.Bank;

namespace FreelanceFlow.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DesignerAccount> DesignerAccounts => Set<DesignerAccount>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectBrief> ProjectBriefs => Set<ProjectBrief>();
    public DbSet<Deliverable> Deliverables => Set<Deliverable>();
    public DbSet<ClientRequest> ClientRequests => Set<ClientRequest>();
    public DbSet<RevisionRound> RevisionRounds => Set<RevisionRound>();
    public DbSet<RevisionItem> RevisionItems => Set<RevisionItem>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    public DbSet<TaskNote> TaskNotes => Set<TaskNote>();
    public DbSet<QuotationSequence> QuotationSequences => Set<QuotationSequence>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<ProjectFile> Files => Set<ProjectFile>();
    public DbSet<StorageAlert> StorageAlerts => Set<StorageAlert>();
    public DbSet<DashboardState> DashboardStates => Set<DashboardState>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
