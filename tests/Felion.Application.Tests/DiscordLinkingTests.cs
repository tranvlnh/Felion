using Felion.Application.Discord;
using Felion.Application.Hardening;
using Felion.Domain.Audit;
using Felion.Domain.Identity;

namespace Felion.Application.Tests;

public sealed class DiscordLinkingTests
{
    [Fact]
    public async Task LinkNormalizesStudentIdAndWritesAuditAndSyncJob()
    {
        var store = new FakeDiscordLinkStore
        {
            Subjects = [new DiscordLinkSubject(
                Guid.NewGuid(),
                DiscordIdentitySubjectType.Member,
                "SV001",
                "Student One")]
        };
        var service = new DiscordLinkingService(store, new TestRateLimitGate());

        var result = await service.LinkAsync(12345, " sv001 ", "correlation-1", CancellationToken.None);

        Assert.Equal("SV001", result.StudentId);
        Assert.Equal(12345, result.DiscordUserId);
        Assert.True(result.SyncQueued);
        Assert.Single(store.Links);
        Assert.Contains(store.AuditLogs, audit => audit.Action == "DiscordIdentityLinked");
        Assert.Single(store.SyncJobs);
        Assert.Equal(DiscordSyncJobStatus.Pending, store.SyncJobs[0].Status);
    }

    [Fact]
    public async Task LinkRejectsUnknownStudentId()
    {
        var service = new DiscordLinkingService(new FakeDiscordLinkStore(), new TestRateLimitGate());

        await Assert.ThrowsAsync<DiscordLinkSubjectNotFoundException>(() => service.LinkAsync(
            12345,
            "SV001",
            "correlation-2",
            CancellationToken.None));
    }

    [Fact]
    public async Task LinkRejectsAmbiguousActiveIdentities()
    {
        var store = new FakeDiscordLinkStore
        {
            Subjects =
            [
                new DiscordLinkSubject(Guid.NewGuid(), DiscordIdentitySubjectType.Member, "SV001", "Member One"),
                new DiscordLinkSubject(Guid.NewGuid(), DiscordIdentitySubjectType.Probation, "SV001", "Candidate One")
            ]
        };
        var service = new DiscordLinkingService(store, new TestRateLimitGate());

        await Assert.ThrowsAsync<DiscordLinkSubjectAmbiguousException>(() => service.LinkAsync(
            12345,
            "SV001",
            "correlation-3",
            CancellationToken.None));
    }

    [Fact]
    public async Task LinkRejectsStudentIdAlreadyLinkedToAnotherDiscordUser()
    {
        var subjectId = Guid.NewGuid();
        var store = new FakeDiscordLinkStore
        {
            Subjects = [new DiscordLinkSubject(subjectId, DiscordIdentitySubjectType.Member, "SV001", "Student One")]
        };
        store.Links.Add(DiscordIdentityLink.Create(54321, "SV001", DiscordIdentitySubjectType.Member, subjectId));
        var service = new DiscordLinkingService(store, new TestRateLimitGate());

        await Assert.ThrowsAsync<DiscordLinkConflictException>(() => service.LinkAsync(
            12345,
            "SV001",
            "correlation-4",
            CancellationToken.None));
    }

    [Fact]
    public async Task LinkRejectsDiscordUserAlreadyLinkedToAnotherIdentity()
    {
        var existingSubjectId = Guid.NewGuid();
        var requestedSubjectId = Guid.NewGuid();
        var store = new FakeDiscordLinkStore
        {
            Subjects = [new DiscordLinkSubject(requestedSubjectId, DiscordIdentitySubjectType.Member, "SV002", "Student Two")]
        };
        store.Links.Add(DiscordIdentityLink.Create(
            12345,
            "SV001",
            DiscordIdentitySubjectType.Member,
            existingSubjectId));
        var service = new DiscordLinkingService(store, new TestRateLimitGate());

        await Assert.ThrowsAsync<DiscordLinkConflictException>(() => service.LinkAsync(
            12345,
            "SV002",
            "correlation-5",
            CancellationToken.None));
    }

    [Fact]
    public async Task LinkIsIdempotentForTheSameIdentity()
    {
        var subjectId = Guid.NewGuid();
        var existingLink = DiscordIdentityLink.Create(
            12345,
            "SV001",
            DiscordIdentitySubjectType.Member,
            subjectId);
        var store = new FakeDiscordLinkStore
        {
            Subjects = [new DiscordLinkSubject(subjectId, DiscordIdentitySubjectType.Member, "SV001", "Student One")]
        };
        store.Links.Add(existingLink);
        var service = new DiscordLinkingService(store, new TestRateLimitGate());

        var result = await service.LinkAsync(12345, "SV001", "correlation-6", CancellationToken.None);

        Assert.Equal(existingLink.LinkedAt, result.LinkedAt);
        Assert.False(result.SyncQueued);
        Assert.Empty(store.AuditLogs);
        Assert.Empty(store.SyncJobs);
    }

    [Fact]
    public async Task LinkRejectsRequestsWhenTheDiscordUserRateLimitIsReached()
    {
        var store = new FakeDiscordLinkStore();
        var rateLimitGate = new TestRateLimitGate(isAcquired: false, retryAfter: TimeSpan.FromMinutes(10));
        var service = new DiscordLinkingService(store, rateLimitGate);

        var exception = await Assert.ThrowsAsync<RateLimitExceededException>(() => service.LinkAsync(
            12345,
            "SV001",
            "correlation-rate-limit",
            CancellationToken.None));

        Assert.Equal(RateLimitOperation.DiscordLink, exception.Operation);
        Assert.Equal(TimeSpan.FromMinutes(10), exception.RetryAfter);
        Assert.Single(rateLimitGate.Attempts);
        Assert.Empty(store.AuditLogs);
        Assert.Empty(store.SyncJobs);
    }

    private sealed class FakeDiscordLinkStore : IDiscordLinkStore
    {
        public List<DiscordLinkSubject> Subjects { get; init; } = [];

        public List<DiscordIdentityLink> Links { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public List<DiscordSyncJob> SyncJobs { get; } = [];

        public Task<IReadOnlyList<DiscordLinkSubject>> FindEligibleSubjectsByStudentIdAsync(
            string normalizedStudentId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<DiscordLinkSubject>>(
                Subjects.Where(subject => subject.StudentId == normalizedStudentId).ToArray());
        }

        public Task<DiscordIdentityLink?> FindByStudentIdAsync(
            string normalizedStudentId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Links.SingleOrDefault(link => link.StudentId == normalizedStudentId));
        }

        public Task<DiscordIdentityLink?> FindByDiscordUserIdAsync(
            long discordUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Links.SingleOrDefault(link => link.DiscordUserId == discordUserId));
        }

        public Task AddAsync(
            DiscordIdentityLink link,
            AuditLog auditLog,
            DiscordSyncJob syncJob,
            CancellationToken cancellationToken)
        {
            if (Links.Any(existing => existing.StudentId == link.StudentId || existing.DiscordUserId == link.DiscordUserId))
            {
                throw new DiscordLinkConflictException("The identity is already linked.");
            }

            Links.Add(link);
            AuditLogs.Add(auditLog);
            SyncJobs.Add(syncJob);
            return Task.CompletedTask;
        }
    }
}
