using Felion.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class DiscordSyncJobConfiguration : IEntityTypeConfiguration<DiscordSyncJob>
{
    public void Configure(EntityTypeBuilder<DiscordSyncJob> builder)
    {
        builder.ToTable(
            "discord_sync_jobs",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_discord_sync_jobs_subject_type",
                    "subject_type IN ('Member', 'Probation')");
                table.HasCheckConstraint(
                    "ck_discord_sync_jobs_operation",
                    "operation IN ('SynchronizeRoles', 'ClearManagedRoles')");
                table.HasCheckConstraint(
                    "ck_discord_sync_jobs_status",
                    "status IN ('Pending', 'Running', 'Succeeded', 'Failed')");
                table.HasCheckConstraint(
                    "ck_discord_sync_jobs_attempts",
                    "attempts >= 0");
            });

        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasColumnName("id");
        builder.Property(job => job.SubjectType).HasColumnName("subject_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(job => job.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(job => job.Operation).HasColumnName("operation").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(job => job.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.Property(job => job.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(job => job.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(job => job.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.Property(job => job.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(job => job.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(job => new { job.Status, job.CreatedAt });
        builder.HasIndex(job => new { job.SubjectType, job.SubjectId, job.Operation, job.Status });
    }
}
