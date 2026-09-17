using Felion.Domain.Evaluation;
using Felion.Domain.Events;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Felion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Felion.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void CorePersistenceModelContainsRequiredUniqueConstraints()
    {
        var options = new DbContextOptionsBuilder<FelionDbContext>()
            .UseNpgsql("Host=localhost;Database=felion;Username=felion;Password=design-time-only")
            .Options;

        using var context = new FelionDbContext(options);

        AssertUniqueIndex<Member>(context, nameof(Member.StudentId));
        AssertUniqueIndex<Member>(context, nameof(Member.ClubEmail));
        AssertUniqueIndex<ProbationCandidate>(context, nameof(ProbationCandidate.StudentId));
        AssertUniqueIndex<DiscordIdentityLink>(context, nameof(DiscordIdentityLink.DiscordUserId));
        AssertUniqueIndex<DiscordIdentityLink>(context, nameof(DiscordIdentityLink.StudentId));
        AssertUniqueIndex<DiscordIdentityLink>(context, nameof(DiscordIdentityLink.SubjectId));

        AssertUniqueIndex<DiscordRoleMapping>(context, nameof(DiscordRoleMapping.Kind), nameof(DiscordRoleMapping.SubjectKey));
        AssertUniqueIndex<DiscordRoleAssignment>(
            context,
            nameof(DiscordRoleAssignment.SubjectType),
            nameof(DiscordRoleAssignment.SubjectId),
            nameof(DiscordRoleAssignment.DiscordRoleId));
        AssertUniqueIndex<PeerEvaluation>(
            context,
            nameof(PeerEvaluation.EvaluationPeriodId),
            nameof(PeerEvaluation.EvaluatorCandidateId),
            nameof(PeerEvaluation.TargetCandidateId));
        AssertUniqueIndex<MentorEvaluation>(
            context,
            nameof(MentorEvaluation.EvaluationPeriodId),
            nameof(MentorEvaluation.MentorMemberId),
            nameof(MentorEvaluation.TargetCandidateId));

        var evaluationPeriod = context.Model.FindEntityType(typeof(EvaluationPeriod));
        Assert.NotNull(evaluationPeriod);
        Assert.Contains(evaluationPeriod!.GetIndexes(), index =>
            index.Properties.Count == 1
            && index.Properties[0].Name == nameof(EvaluationPeriod.Status));

        var teamMentor = context.Model.FindEntityType(typeof(TeamMentor));
        Assert.NotNull(teamMentor);
        Assert.Equal(
            [nameof(TeamMentor.TeamId), nameof(TeamMentor.MemberId)],
            teamMentor!.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(teamMentor.GetIndexes(), index =>
            index.Properties.Count == 1
            && index.Properties[0].Name == nameof(TeamMentor.MemberId));

        var syncJob = context.Model.FindEntityType(typeof(DiscordSyncJob));
        Assert.NotNull(syncJob);
        Assert.Contains(syncJob!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [
                    nameof(DiscordSyncJob.Status),
                    nameof(DiscordSyncJob.NextAttemptAt),
                    nameof(DiscordSyncJob.CreatedAt)
                ]));

        var @event = context.Model.FindEntityType(typeof(Event));
        Assert.NotNull(@event);
        Assert.Contains(@event!.GetIndexes(), index =>
            index.Properties.Count == 1
            && index.Properties[0].Name == nameof(Event.Status));

        var eventPosition = context.Model.FindEntityType(typeof(EventPosition));
        Assert.NotNull(eventPosition);
        Assert.Contains(eventPosition!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(EventPosition.EventId), nameof(EventPosition.SortOrder)]));

        AssertUniqueIndex<EventRegistration>(
            context,
            nameof(EventRegistration.EventPositionId),
            nameof(EventRegistration.MemberId));
        AssertUniqueIndex<EventAttendance>(
            context,
            nameof(EventAttendance.EventId),
            nameof(EventAttendance.MemberId));
    }

    [Fact]
    public void CoreDepartmentIsSeededAsTheOnlyCoreDepartment()
    {
        var options = new DbContextOptionsBuilder<FelionDbContext>()
            .UseNpgsql("Host=localhost;Database=felion;Username=felion;Password=design-time-only")
            .Options;

        using var context = new FelionDbContext(options);
        var department = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Department));
        var seed = department?.GetSeedData().Single();

        Assert.NotNull(seed);
        Assert.Equal(Department.CoreDepartmentId, seed[nameof(Department.Id)]);
        Assert.Equal("core", seed[nameof(Department.Slug)]);
        Assert.Equal(true, seed[nameof(Department.IsCore)]);
    }

    private static void AssertUniqueIndex<TEntity>(FelionDbContext context, params string[] propertyNames)
        where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        var index = entityType?.GetIndexes().SingleOrDefault(candidate =>
            candidate.Properties.Count == propertyNames.Length
            && candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }
}
