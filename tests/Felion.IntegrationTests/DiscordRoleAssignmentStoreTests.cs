using Felion.Application.Discord;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Felion.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Felion.IntegrationTests;

public sealed class DiscordRoleAssignmentStoreTests
{
    [PostgreSqlFact]
    public async Task ListSubjectsAsyncReturnsActiveMembersAndCandidates()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var department = Department.CreateRegular("Role Assignment Test", "role-assignment-test");
        var generation = Generation.Create("Role Assignment Test", "ROLE-ASSIGNMENT-TEST");
        var member = Member.Create(
            "MEMBER-ROLE-001",
            "Role Assignment Member",
            "member-role-001@example.org",
            department.Id,
            isCoreDepartment: false,
            generation.Id,
            MemberPosition.Member);
        var candidate = ProbationCandidate.Create(
            "CANDIDATE-ROLE-001",
            "Role Assignment Candidate",
            department.Id,
            generation.Id);
        var assignment = DiscordRoleAssignment.Create(
            DiscordIdentitySubjectType.Member,
            member.Id,
            987654321,
            "Custom Role");

        await using (var context = database.CreateContext())
        {
            context.Departments.Add(department);
            context.Generations.Add(generation);
            context.Members.Add(member);
            context.ProbationCandidates.Add(candidate);
            context.DiscordIdentityLinks.Add(DiscordIdentityLink.Create(
                123456789,
                member.StudentId,
                DiscordIdentitySubjectType.Member,
                member.Id));
            context.DiscordRoleAssignments.Add(assignment);
            await context.SaveChangesAsync();
        }

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
        var store = scope.ServiceProvider.GetRequiredService<IDiscordRoleAssignmentStore>();

        var result = await store.ListSubjectsAsync(CancellationToken.None);

        var memberSubject = Assert.Single(result, subject => subject.SubjectId == member.Id);
        Assert.Equal(DiscordIdentitySubjectType.Member, memberSubject.SubjectType);
        Assert.Equal(123456789, memberSubject.DiscordUserId);
        var memberAssignment = Assert.Single(memberSubject.Assignments);
        Assert.Equal(987654321, memberAssignment.DiscordRoleId);
        Assert.Equal("Custom Role", memberAssignment.RoleNameSnapshot);

        var candidateSubject = Assert.Single(result, subject => subject.SubjectId == candidate.Id);
        Assert.Equal(DiscordIdentitySubjectType.Probation, candidateSubject.SubjectType);
        Assert.Null(candidateSubject.DiscordUserId);
        Assert.Empty(candidateSubject.Assignments);
    }
}
