using Felion.Domain.Events;
using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable(
            "events",
            table => table.HasCheckConstraint(
                "ck_events_status",
                "status IN ('Draft', 'Published', 'RegistrationClosed', 'InProgress', 'Completed', 'Cancelled')"));

        builder.HasKey(@event => @event.Id);
        builder.Property(@event => @event.Id).HasColumnName("id");
        builder.Property(@event => @event.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(@event => @event.Description).HasColumnName("description");
        builder.Property(@event => @event.Location).HasColumnName("location");
        builder.Property(@event => @event.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(@event => @event.EndsAt).HasColumnName("ends_at");
        builder.Property(@event => @event.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(@event => @event.AllowMultiplePositions).HasColumnName("allow_multiple_positions").IsRequired();
        builder.Property(@event => @event.CreatedByMemberId).HasColumnName("created_by_member_id").IsRequired();
        builder.Property(@event => @event.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(@event => @event.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();

        builder.HasIndex(@event => @event.Status);
        builder.HasIndex(@event => @event.StartsAt);
        builder.HasIndex(@event => @event.CreatedByMemberId);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(@event => @event.CreatedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
