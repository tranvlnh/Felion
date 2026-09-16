using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable(
            "departments",
            table => table.HasCheckConstraint(
                "ck_departments_core_slug",
                "NOT is_core OR slug = 'core'"));

        builder.HasKey(department => department.Id);
        builder.Property(department => department.Id).HasColumnName("id");
        builder.Property(department => department.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(department => department.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        builder.Property(department => department.IsCore).HasColumnName("is_core");
        builder.Property(department => department.IsActive).HasColumnName("is_active");
        builder.HasIndex(department => department.Slug).IsUnique();
        builder.HasIndex(department => department.IsCore).IsUnique().HasFilter("is_core = true");

        builder.HasData(new
        {
            Id = Department.CoreDepartmentId,
            Name = "Core",
            Slug = "core",
            IsCore = true,
            IsActive = true
        });
    }
}
