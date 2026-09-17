using Felion.Domain.Audit;
using Felion.Domain.Evaluation;
using Felion.Domain.Events;
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

    public DbSet<DiscordRoleAssignment> DiscordRoleAssignments => Set<DiscordRoleAssignment>();

    public DbSet<EvaluationPeriod> EvaluationPeriods => Set<EvaluationPeriod>();

    public DbSet<EvaluationForm> EvaluationForms => Set<EvaluationForm>();

    public DbSet<EvaluationQuestion> EvaluationQuestions => Set<EvaluationQuestion>();

    public DbSet<EvaluationSubmission> EvaluationSubmissions => Set<EvaluationSubmission>();

    public DbSet<EvaluationAnswer> EvaluationAnswers => Set<EvaluationAnswer>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventPosition> EventPositions => Set<EventPosition>();

    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();

    public DbSet<EventAttendance> EventAttendances => Set<EventAttendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FelionDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
