using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class TeamMentorConfiguration : IEntityTypeConfiguration<TeamMentor>
{
    public void Configure(EntityTypeBuilder<TeamMentor> builder)
    {
        builder.ToTable("team_mentors");

        builder.HasKey(mentor => new { mentor.TeamId, mentor.MemberId });
        builder.Property(mentor => mentor.TeamId).HasColumnName("team_id");
        builder.Property(mentor => mentor.MemberId).HasColumnName("member_id");

        builder.HasIndex(mentor => mentor.MemberId);

        builder.HasOne<ProbationTeam>()
            .WithMany()
            .HasForeignKey(mentor => mentor.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(mentor => mentor.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
