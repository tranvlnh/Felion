using Felion.Application.Probation;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Felion.Infrastructure;
using Felion.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Felion.IntegrationTests;

public sealed class ProbationCandidateStoreTests
{
    [PostgreSqlFact]
    public async Task ListAsyncOrdersCandidatesBeforeProjectingViews()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (departmentId, generationId, _) = await SeedCandidatesAsync(database);

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = database.ConnectionString
            })
            .Build();
        services.AddFelionInfrastructure(configuration, services.AddHealthChecks());

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IProbationCandidateStore>();

        var result = await store.ListAsync(
            new ListProbationCandidatesQuery(Page: 1, PageSize: 10),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(
            ["CANDIDATE-001", "CANDIDATE-002"],
            result.Items.Select(item => item.Candidate.StudentId).ToArray());
        Assert.All(result.Items, item =>
        {
            Assert.Equal(departmentId, item.Department.Id);
            Assert.Equal(generationId, item.Generation.Id);
        });
    }

    [PostgreSqlFact]
    public async Task FindViewAsyncReturnsCandidateWithoutTeamOrDiscordLink()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (_, _, candidateId) = await SeedCandidatesAsync(database);

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = database.ConnectionString
            })
            .Build();
        services.AddFelionInfrastructure(configuration, services.AddHealthChecks());

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IProbationCandidateStore>();

        var result = await store.FindViewAsync(candidateId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(candidateId, result.Candidate.Id);
        Assert.Null(result.Team);
        Assert.False(result.HasDiscordIdentity);
    }

    private static async Task<(Guid DepartmentId, Guid GenerationId, Guid CandidateId)> SeedCandidatesAsync(
        PostgreSqlTestDatabase database)
    {
        await using var context = database.CreateContext();
        var department = Department.CreateRegular("Probation Store Test", "probation-store-test");
        var generation = Generation.Create("Probation Store Test", "PROBATION-STORE-TEST");
        var first = ProbationCandidate.Create(
            "CANDIDATE-002",
            "Second Candidate",
            department.Id,
            generation.Id);
        var second = ProbationCandidate.Create(
            "CANDIDATE-001",
            "First Candidate",
            department.Id,
            generation.Id);

        context.Departments.Add(department);
        context.Generations.Add(generation);
        context.ProbationCandidates.AddRange(first, second);
        await context.SaveChangesAsync();

        return (department.Id, generation.Id, first.Id);
    }
}
