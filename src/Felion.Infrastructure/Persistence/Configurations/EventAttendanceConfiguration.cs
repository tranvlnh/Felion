using Felion.Domain.Events;
using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class EventAttendanceConfiguration : IEntityTypeConfiguration<EventAttendance>
{
    public void Configure(EntityTypeBuilder<EventAttendance> builder)
    {
        builder.ToTable("event_attendances");

        builder.HasKey(attendance => attendance.Id);
        builder.Property(attendance => attendance.Id).HasColumnName("id");
        builder.Property(attendance => attendance.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(attendance => attendance.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(attendance => attendance.CheckedInAt).HasColumnName("checked_in_at").IsRequired();
        builder.Property(attendance => attendance.CheckedInByMemberId).HasColumnName("checked_in_by_member_id").IsRequired();
        builder.Property(attendance => attendance.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(attendance => new { attendance.EventId, attendance.MemberId }).IsUnique();
        builder.HasIndex(attendance => attendance.MemberId);

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(attendance => attendance.EventId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(attendance => attendance.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(attendance => attendance.CheckedInByMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
