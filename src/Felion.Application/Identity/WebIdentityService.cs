using Felion.Domain.Common;

namespace Felion.Application.Identity;

public sealed class WebIdentityService(IWebIdentityDirectory directory) : IWebIdentityService
{
    public async Task<WebMemberIdentity?> ResolveActiveMemberByEmailAsync(
        string? email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        try
        {
            var normalizedEmail = IdentityNormalizer.ClubEmail(email);
            return await directory.FindActiveByClubEmailAsync(normalizedEmail, cancellationToken);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    public Task<WebMemberIdentity?> ResolveActiveMemberByIdAsync(
        Guid memberId,
        CancellationToken cancellationToken)
    {
        return memberId == Guid.Empty
            ? Task.FromResult<WebMemberIdentity?>(null)
            : directory.FindActiveByIdAsync(memberId, cancellationToken);
    }
}
