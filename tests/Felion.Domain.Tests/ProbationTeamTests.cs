using Felion.Domain.Common;
using Felion.Domain.Probation;

namespace Felion.Domain.Tests;

public sealed class ProbationTeamTests
{
    [Fact]
    public void CreateNormalizesNameAndStartsActive()
    {
        var team = ProbationTeam.Create(" Team Alpha ");

        Assert.Equal("Team Alpha", team.Name);
        Assert.True(team.IsActive);
    }

    [Fact]
    public void CreateRejectsAnEmptyOrOverlongName()
    {
        Assert.Throws<DomainException>(() => ProbationTeam.Create(" "));
        Assert.Throws<DomainException>(() => ProbationTeam.Create(new string('x', 201)));
    }

    [Fact]
    public void TeamMentorRequiresBothTeamAndMember()
    {
        Assert.Throws<DomainException>(() => TeamMentor.Create(Guid.Empty, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => TeamMentor.Create(Guid.NewGuid(), Guid.Empty));
    }
}
