using Felion.Domain.Events;
using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class EventRegistrationConfiguration : IEntityTypeConfiguration<EventRegistration>
{
    public void Configure(EntityTypeBuilder<EventRegistration> builder)
    {
        builder.ToTable(
            "event_registrations",
            table => table.HasCheckConstraint(
                "ck_event_registrations_status",
                "status IN ('Pending', 'Approved', 'Rejected', 'Cancelled', 'Assigned')"));

        builder.HasKey(registration => registration.Id);
        builder.Property(registration => registration.Id).HasColumnName("id");
        builder.Property(registration => registration.EventPositionId).HasColumnName("event_position_id").IsRequired();
        builder.Property(registration => registration.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(registration => registration.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(registration => registration.RequestedAt).HasColumnName("requested_at");
        builder.Property(registration => registration.DecidedAt).HasColumnName("decided_at");
        builder.Property(registration => registration.DecidedByMemberId).HasColumnName("decided_by_member_id");
        builder.Property(registration => registration.AssignedAt).HasColumnName("assigned_at");
        builder.Property(registration => registration.AssignedByMemberId).HasColumnName("assigned_by_member_id");
        builder.Property(registration => registration.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(registration => registration.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(registration => registration.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();

        builder.HasIndex(registration => new { registration.EventPositionId, registration.MemberId })
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Approved', 'Assigned')");
        builder.HasIndex(registration => new { registration.EventPositionId, registration.Status });
        builder.HasIndex(registration => registration.MemberId);

        builder.HasOne<EventPosition>()
            .WithMany()
            .HasForeignKey(registration => registration.EventPositionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(registration => registration.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(registration => registration.DecidedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(registration => registration.AssignedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
