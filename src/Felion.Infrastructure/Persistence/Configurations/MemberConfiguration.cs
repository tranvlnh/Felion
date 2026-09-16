using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable(
            "members",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_members_position",
                    "position IN ('Admin', 'Core', 'Member')");
                table.HasCheckConstraint(
                    "ck_members_status",
                    "status IN ('Active', 'Inactive')");
            });

        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).HasColumnName("id");
        builder.Property(member => member.StudentId).HasColumnName("student_id").HasMaxLength(50).IsRequired();
        builder.Property(member => member.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(member => member.ClubEmail).HasColumnName("club_email").HasMaxLength(320).IsRequired();
        builder.Property(member => member.DepartmentId).HasColumnName("department_id");
        builder.Property(member => member.GenerationId).HasColumnName("generation_id");
        builder.Property(member => member.Position).HasColumnName("position").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(member => member.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(member => member.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(member => member.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(member => member.StudentId).IsUnique();
        builder.HasIndex(member => member.ClubEmail).IsUnique();
        builder.HasIndex(member => member.DepartmentId);
        builder.HasIndex(member => member.GenerationId);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(member => member.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Generation>()
            .WithMany()
            .HasForeignKey(member => member.GenerationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
