using Felion.Domain.Events;
using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class EventPositionConfiguration : IEntityTypeConfiguration<EventPosition>
{
    public void Configure(EntityTypeBuilder<EventPosition> builder)
    {
        builder.ToTable(
            "event_positions",
            table =>
            {
                table.HasCheckConstraint("ck_event_positions_capacity", "capacity > 0");
                table.HasCheckConstraint("ck_event_positions_sort_order", "sort_order >= 0");
            });

        builder.HasKey(position => position.Id);
        builder.Property(position => position.Id).HasColumnName("id");
        builder.Property(position => position.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(position => position.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(position => position.Description).HasColumnName("description");
        builder.Property(position => position.Capacity).HasColumnName("capacity").IsRequired();
        builder.Property(position => position.RequiredDepartmentId).HasColumnName("required_department_id");
        builder.Property(position => position.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(position => position.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(position => position.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();

        builder.HasIndex(position => new { position.EventId, position.SortOrder });
        builder.HasIndex(position => position.RequiredDepartmentId);

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(position => position.EventId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(position => position.RequiredDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
