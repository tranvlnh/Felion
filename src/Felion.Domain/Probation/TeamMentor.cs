using Felion.Domain.Common;

namespace Felion.Domain.Probation;

public sealed class TeamMentor
{
    private TeamMentor()
    {
    }

    private TeamMentor(Guid teamId, Guid memberId)
    {
        TeamId = teamId;
        MemberId = memberId;
    }

    public Guid TeamId { get; private set; }

    public Guid MemberId { get; private set; }

    public static TeamMentor Create(Guid teamId, Guid memberId)
    {
        if (teamId == Guid.Empty)
        {
            throw new DomainException("Team is required.");
        }

        if (memberId == Guid.Empty)
        {
            throw new DomainException("Mentor member is required.");
        }

        return new TeamMentor(teamId, memberId);
    }
}
