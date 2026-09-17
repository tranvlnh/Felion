using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class DiscordAuthorizationTests
{
    [Fact]
    public async Task LinkedActiveAdminIsAuthorized()
    {
        var context = TestContext.Create(MemberPosition.Admin, DiscordIdentitySubjectType.Member);
        var service = new DiscordAuthorizationService(context.LinkStore, context.MemberStore);

        var actor = await service.RequireAdminAsync(123456789, CancellationToken.None);

        Assert.Equal(context.Member.Id, actor.MemberId);
        Assert.Equal(123456789, actor.DiscordUserId);
    }

    [Theory]
    [InlineData(MemberPosition.Core, DiscordIdentitySubjectType.Member)]
    [InlineData(MemberPosition.Admin, DiscordIdentitySubjectType.Probation)]
    public async Task NonAdminOrProbationLinkIsRejected(
        MemberPosition position,
        DiscordIdentitySubjectType subjectType)
    {
        var context = TestContext.Create(position, subjectType);
        var service = new DiscordAuthorizationService(context.LinkStore, context.MemberStore);

        await Assert.ThrowsAsync<DiscordAuthorizationException>(() => service.RequireAdminAsync(
            123456789,
            CancellationToken.None));
    }

    [Fact]
    public async Task UnlinkedDiscordUserIsRejected()
    {
        var context = TestContext.Create(MemberPosition.Admin, DiscordIdentitySubjectType.Member);
        context.LinkStore.Link = null;
        var service = new DiscordAuthorizationService(context.LinkStore, context.MemberStore);

        await Assert.ThrowsAsync<DiscordAuthorizationException>(() => service.RequireAdminAsync(
            123456789,
            CancellationToken.None));
    }

    private sealed class TestContext
    {
        private TestContext(Member member, FakeMemberStore memberStore, FakeDiscordLinkStore linkStore)
        {
            Member = member;
            MemberStore = memberStore;
            LinkStore = linkStore;
        }

        public Member Member { get; }

        public FakeMemberStore MemberStore { get; }

        public FakeDiscordLinkStore LinkStore { get; }

        public static TestContext Create(
            MemberPosition position,
            DiscordIdentitySubjectType subjectType)
        {
            var department = Department.CreateCore();
            var generation = Generation.Create("Generation 1", "G1");
            var member = Member.Create(
                "ADMIN001",
                "Admin",
                "admin@example.org",
                department.Id,
                isCoreDepartment: true,
                generation.Id,
                position);
            var memberStore = new FakeMemberStore();
            memberStore.Members.Add(member);
            var linkStore = new FakeDiscordLinkStore
            {
                Link = DiscordIdentityLink.Create(
                    123456789,
                    member.StudentId,
                    subjectType,
                    member.Id)
            };
            return new TestContext(member, memberStore, linkStore);
        }
    }

    private sealed class FakeDiscordLinkStore : IDiscordLinkStore
    {
        public DiscordIdentityLink? Link { get; set; }

        public Task<DiscordIdentityLink?> FindByDiscordUserIdAsync(
            long discordUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Link?.DiscordUserId == discordUserId ? Link : null);
        }

        public Task<IReadOnlyList<DiscordLinkSubject>> FindEligibleSubjectsByStudentIdAsync(
            string normalizedStudentId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DiscordIdentityLink?> FindByStudentIdAsync(
            string normalizedStudentId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddAsync(
            DiscordIdentityLink link,
            AuditLog auditLog,
            Felion.Domain.Identity.DiscordSyncJob syncJob,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeMemberStore : IMemberStore
    {
        public List<Member> Members { get; } = [];

        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Members.SingleOrDefault(member => member.Id == memberId));
        }

        public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(
            int page,
            int pageSize,
            MemberStatus? status,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(
            IReadOnlyCollection<string> studentIds,
            IReadOnlyCollection<string> clubEmails,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task UpdateAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddRangeAsync(
            IReadOnlyCollection<MemberAuditEntry> entries,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
