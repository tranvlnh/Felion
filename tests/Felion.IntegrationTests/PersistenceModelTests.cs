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

    private static void AssertUniqueIndex<TEntity>(FelionDbContext context, string propertyName)
        where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        var index = entityType?.GetIndexes().SingleOrDefault(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == propertyName);

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }
}
