using Felion.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class DiscordRoleMappingConfiguration : IEntityTypeConfiguration<DiscordRoleMapping>
{
    public void Configure(EntityTypeBuilder<DiscordRoleMapping> builder)
    {
        builder.ToTable(
            "discord_role_mappings",
            table => table.HasCheckConstraint(
                "ck_discord_role_mappings_kind",
                "kind IN ('Position', 'Probation', 'Department', 'Generation', 'ProbationTeam')"));

        builder.HasKey(mapping => mapping.Id);
        builder.Property(mapping => mapping.Id).HasColumnName("id");
        builder.Property(mapping => mapping.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(mapping => mapping.SubjectKey).HasColumnName("subject_key").HasMaxLength(100).IsRequired();
        builder.Property(mapping => mapping.DiscordRoleId).HasColumnName("discord_role_id").IsRequired();
        builder.Property(mapping => mapping.RoleNameSnapshot).HasColumnName("role_name_snapshot").HasMaxLength(100).IsRequired();
        builder.Property(mapping => mapping.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(mapping => new { mapping.Kind, mapping.SubjectKey }).IsUnique();
    }
}
