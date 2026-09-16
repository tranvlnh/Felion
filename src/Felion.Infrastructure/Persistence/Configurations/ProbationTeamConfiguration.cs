using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class ProbationTeamConfiguration : IEntityTypeConfiguration<ProbationTeam>
{
    public void Configure(EntityTypeBuilder<ProbationTeam> builder)
    {
        builder.ToTable("probation_teams");

        builder.HasKey(team => team.Id);
        builder.Property(team => team.Id).HasColumnName("id");
        builder.Property(team => team.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(team => team.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(team => team.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(team => team.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();
    }
}
