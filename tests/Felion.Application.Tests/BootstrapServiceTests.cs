using Felion.Application.Bootstrap;
using Felion.Domain.Audit;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class BootstrapServiceTests
{
    [Fact]
    public async Task BootstrapAdminCreatesInitialDataAndSystemAudit()
    {
        var store = new FakeBootstrapStore();
        var service = new BootstrapService(store);

        var result = await service.BootstrapAdminAsync(CreateCommand());

        Assert.Equal("admin@gdscptit.dev", result.ClubEmail);
        Assert.Equal("Gen 1", result.GenerationName);
        Assert.NotNull(store.SavedMember);
        Assert.Equal(MemberPosition.Admin, store.SavedMember.Position);
        Assert.Equal(MemberStatus.Active, store.SavedMember.Status);
        Assert.Equal(Department.CoreDepartmentId, store.SavedMember.DepartmentId);
        Assert.Equal("GEN1", store.SavedGeneration?.Code);
        Assert.Equal(2, store.SavedAuditLogs.Count);
        Assert.All(store.SavedAuditLogs, audit => Assert.Equal(AuditActorType.System, audit.ActorType));
    }

    [Fact]
    public async Task BootstrapAdminRejectsIncompleteConfiguration()
    {
        var service = new BootstrapService(new FakeBootstrapStore());
        var command = CreateCommand() with { ClubEmail = string.Empty };

        await Assert.ThrowsAsync<ArgumentException>(() => service.BootstrapAdminAsync(command));
    }

    [Fact]
    public async Task BootstrapAdminRejectsNonEmptyDatabase()
    {
        var service = new BootstrapService(new FakeBootstrapStore(hasMembers: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BootstrapAdminAsync(CreateCommand()));
    }

    private static BootstrapAdminCommand CreateCommand()
    {
        return new BootstrapAdminCommand(
            "B21DCCN001",
            "Admin User",
            "admin@gdscptit.dev",
            "Gen 1",
            "gen1");
    }

    private sealed class FakeBootstrapStore(bool hasMembers = false) : IBootstrapStore
    {
        public Member? SavedMember { get; private set; }

        public Generation? SavedGeneration { get; private set; }

        public List<AuditLog> SavedAuditLogs { get; } = [];

        public Task<bool> HasAnyMembersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(hasMembers);
        }

        public Task<Department?> FindDepartmentByIdAsync(Guid departmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Department?>(Department.CreateCore());
        }

        public Task<Generation?> FindGenerationByCodeAsync(string code, CancellationToken cancellationToken)
        {
            return Task.FromResult<Generation?>(null);
        }

        public Task SaveBootstrapAsync(
            Generation? newGeneration,
            Member adminMember,
            IReadOnlyCollection<AuditLog> auditLogs,
            CancellationToken cancellationToken)
        {
            SavedGeneration = newGeneration;
            SavedMember = adminMember;
            SavedAuditLogs.AddRange(auditLogs);
            return Task.CompletedTask;
        }
    }
}
