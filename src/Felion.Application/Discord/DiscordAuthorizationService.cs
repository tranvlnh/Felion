using Felion.Application.Members;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed class DiscordAuthorizationService(
    IDiscordLinkStore linkStore,
    IMemberStore memberStore) : IDiscordAuthorizationService
{
    public async Task<DiscordAdminActor?> FindAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        if (discordUserId <= 0)
        {
            return null;
        }

        var link = await linkStore.FindByDiscordUserIdAsync(discordUserId, cancellationToken);
        if (link is null || link.SubjectType != DiscordIdentitySubjectType.Member)
        {
            return null;
        }

        var member = await memberStore.FindByIdAsync(link.SubjectId, track: false, cancellationToken);
        if (member is null
            || member.Status != MemberStatus.Active
            || member.Position != MemberPosition.Admin)
        {
            return null;
        }

        return new DiscordAdminActor(member.Id, discordUserId);
    }

    public async Task<DiscordAdminActor> RequireAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        return await FindAdminAsync(discordUserId, cancellationToken)
            ?? throw new DiscordAuthorizationException();
    }

    public async Task<DiscordManagementActor?> FindCoreOrAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        if (discordUserId <= 0)
        {
            return null;
        }

        var link = await linkStore.FindByDiscordUserIdAsync(discordUserId, cancellationToken);
        if (link is null || link.SubjectType != DiscordIdentitySubjectType.Member)
        {
            return null;
        }

        var member = await memberStore.FindByIdAsync(link.SubjectId, track: false, cancellationToken);
        if (member is null
            || member.Status != MemberStatus.Active
            || member.Position is not (MemberPosition.Admin or MemberPosition.Core))
        {
            return null;
        }

        return new DiscordManagementActor(member.Id, discordUserId, member.Position);
    }

    public async Task<DiscordManagementActor> RequireCoreOrAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        return await FindCoreOrAdminAsync(discordUserId, cancellationToken)
            ?? throw new DiscordAuthorizationException();
    }
}
