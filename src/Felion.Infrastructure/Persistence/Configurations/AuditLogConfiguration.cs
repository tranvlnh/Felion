using Felion.Domain.Audit;
using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable(
            "audit_logs",
            table => table.HasCheckConstraint(
                "ck_audit_logs_actor",
                "(actor_type = 'WebMember' AND actor_member_id IS NOT NULL AND actor_discord_user_id IS NULL) OR "
                + "(actor_type = 'DiscordMember' AND actor_member_id IS NULL AND actor_discord_user_id IS NOT NULL) OR "
                + "(actor_type = 'System' AND actor_member_id IS NULL AND actor_discord_user_id IS NULL)"));

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).HasColumnName("id");
        builder.Property(log => log.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(log => log.ActorType).HasColumnName("actor_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(log => log.ActorMemberId).HasColumnName("actor_member_id");
        builder.Property(log => log.ActorDiscordUserId).HasColumnName("actor_discord_user_id");
        builder.Property(log => log.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(log => log.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        builder.Property(log => log.EntityId).HasColumnName("entity_id");
        builder.Property(log => log.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(log => log.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb").IsRequired();
        builder.Property(log => log.BeforeJson).HasColumnName("before_json").HasColumnType("jsonb");
        builder.Property(log => log.AfterJson).HasColumnName("after_json").HasColumnType("jsonb");

        builder.HasIndex(log => log.OccurredAt);
        builder.HasIndex(log => new { log.EntityType, log.EntityId });
        builder.HasIndex(log => log.CorrelationId);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(log => log.ActorMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
