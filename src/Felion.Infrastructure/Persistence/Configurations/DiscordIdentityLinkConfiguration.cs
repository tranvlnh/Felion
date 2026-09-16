using Felion.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class DiscordIdentityLinkConfiguration : IEntityTypeConfiguration<DiscordIdentityLink>
{
    public void Configure(EntityTypeBuilder<DiscordIdentityLink> builder)
    {
        builder.ToTable(
            "discord_identity_links",
            table => table.HasCheckConstraint(
                "ck_discord_identity_links_subject_type",
                "subject_type IN ('Member', 'Probation')"));

        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).HasColumnName("id");
        builder.Property(link => link.DiscordUserId).HasColumnName("discord_user_id").IsRequired();
        builder.Property(link => link.StudentId).HasColumnName("student_id").HasMaxLength(50).IsRequired();
        builder.Property(link => link.SubjectType).HasColumnName("subject_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(link => link.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(link => link.LinkedAt).HasColumnName("linked_at").IsRequired();

        builder.HasIndex(link => link.DiscordUserId).IsUnique();
        builder.HasIndex(link => link.StudentId).IsUnique();
        builder.HasIndex(link => link.SubjectId).IsUnique();
    }
}
