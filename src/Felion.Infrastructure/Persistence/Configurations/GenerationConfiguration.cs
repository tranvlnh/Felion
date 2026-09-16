using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class GenerationConfiguration : IEntityTypeConfiguration<Generation>
{
    public void Configure(EntityTypeBuilder<Generation> builder)
    {
        builder.ToTable("generations");
        builder.HasKey(generation => generation.Id);
        builder.Property(generation => generation.Id).HasColumnName("id");
        builder.Property(generation => generation.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(generation => generation.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(generation => generation.IsActive).HasColumnName("is_active");
        builder.HasIndex(generation => generation.Code).IsUnique();
    }
}
