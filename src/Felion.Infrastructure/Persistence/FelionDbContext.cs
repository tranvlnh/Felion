using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;

namespace Felion.Infrastructure.Persistence;

public sealed class FelionDbContext(DbContextOptions<FelionDbContext> options) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Generation> Generations => Set<Generation>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<ProbationCandidate> ProbationCandidates => Set<ProbationCandidate>();

    public DbSet<ProbationTeam> ProbationTeams => Set<ProbationTeam>();

    public DbSet<TeamMentor> TeamMentors => Set<TeamMentor>();

    public DbSet<DiscordIdentityLink> DiscordIdentityLinks => Set<DiscordIdentityLink>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<DiscordSyncJob> DiscordSyncJobs => Set<DiscordSyncJob>();

    public DbSet<DiscordRoleMapping> DiscordRoleMappings => Set<DiscordRoleMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FelionDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
