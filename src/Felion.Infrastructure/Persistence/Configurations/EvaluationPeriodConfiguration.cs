using Felion.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class EvaluationPeriodConfiguration : IEntityTypeConfiguration<EvaluationPeriod>
{
    public void Configure(EntityTypeBuilder<EvaluationPeriod> builder)
    {
        builder.ToTable(
            "evaluation_periods",
            table => table.HasCheckConstraint(
                "ck_evaluation_periods_status",
                "status IN ('Draft', 'Open', 'Closed')"));

        builder.HasKey(period => period.Id);
        builder.Property(period => period.Id).HasColumnName("id");
        builder.Property(period => period.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(period => period.StartsAt).HasColumnName("starts_at");
        builder.Property(period => period.EndsAt).HasColumnName("ends_at");
        builder.Property(period => period.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(period => period.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(period => period.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();

        builder.HasIndex(period => period.Status);
    }
}
