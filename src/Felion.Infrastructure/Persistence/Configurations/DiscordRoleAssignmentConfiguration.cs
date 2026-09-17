using Felion.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class DiscordRoleAssignmentConfiguration : IEntityTypeConfiguration<DiscordRoleAssignment>
{
    public void Configure(EntityTypeBuilder<DiscordRoleAssignment> builder)
    {
        builder.ToTable(
            "discord_role_assignments",
            table => table.HasCheckConstraint(
                "ck_discord_role_assignments_subject_type",
                "subject_type IN ('Member', 'Probation')"));

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).HasColumnName("id");
        builder.Property(assignment => assignment.SubjectType)
            .HasColumnName("subject_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(assignment => assignment.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(assignment => assignment.DiscordRoleId).HasColumnName("discord_role_id").IsRequired();
        builder.Property(assignment => assignment.RoleNameSnapshot)
            .HasColumnName("role_name_snapshot")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(assignment => assignment.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(assignment => assignment.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(assignment =>
            new { assignment.SubjectType, assignment.SubjectId, assignment.DiscordRoleId })
            .IsUnique();
        builder.HasIndex(assignment => assignment.DiscordRoleId);
    }
}
