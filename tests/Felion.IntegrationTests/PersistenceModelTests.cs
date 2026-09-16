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
            index.Properties.Count == 2
            && index.Properties[0].Name == nameof(DiscordSyncJob.Status)
            && index.Properties[1].Name == nameof(DiscordSyncJob.CreatedAt));
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
