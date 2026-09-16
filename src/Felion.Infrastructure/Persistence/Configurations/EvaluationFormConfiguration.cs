using Felion.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class EvaluationFormConfiguration : IEntityTypeConfiguration<EvaluationForm>
{
    public void Configure(EntityTypeBuilder<EvaluationForm> builder)
    {
        builder.ToTable(
            "evaluation_forms",
            table => table.HasCheckConstraint(
                "ck_evaluation_forms_reviewer_type",
                "reviewer_type IN ('Peer', 'Mentor')"));

        builder.HasKey(form => form.Id);
        builder.Property(form => form.Id).HasColumnName("id");
        builder.Property(form => form.PeriodId).HasColumnName("period_id").IsRequired();
        builder.Property(form => form.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(form => form.ReviewerType).HasColumnName("reviewer_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(form => form.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(form => form.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(form => form.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();

        builder.HasIndex(form => form.PeriodId);
        builder.HasOne<EvaluationPeriod>()
            .WithMany()
            .HasForeignKey(form => form.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
