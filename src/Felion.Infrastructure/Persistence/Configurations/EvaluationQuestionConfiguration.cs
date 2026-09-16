using Felion.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class EvaluationQuestionConfiguration : IEntityTypeConfiguration<EvaluationQuestion>
{
    public void Configure(EntityTypeBuilder<EvaluationQuestion> builder)
    {
        builder.ToTable(
            "evaluation_questions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_evaluation_questions_configuration",
                    "(type = 'Score' AND score_min IS NOT NULL AND score_max IS NOT NULL AND score_min <= score_max AND text_max_length IS NULL) OR "
                    + "(type = 'Text' AND score_min IS NULL AND score_max IS NULL AND text_max_length IS NOT NULL AND text_max_length > 0)");
                table.HasCheckConstraint("ck_evaluation_questions_order", "order_number > 0");
            });

        builder.HasKey(question => question.Id);
        builder.Property(question => question.Id).HasColumnName("id");
        builder.Property(question => question.FormId).HasColumnName("form_id").IsRequired();
        builder.Property(question => question.Order).HasColumnName("order_number").IsRequired();
        builder.Property(question => question.Prompt).HasColumnName("prompt").HasMaxLength(2000).IsRequired();
        builder.Property(question => question.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(question => question.IsRequired).HasColumnName("is_required").IsRequired();
        builder.Property(question => question.ScoreMin).HasColumnName("score_min").HasPrecision(18, 4);
        builder.Property(question => question.ScoreMax).HasColumnName("score_max").HasPrecision(18, 4);
        builder.Property(question => question.TextMaxLength).HasColumnName("text_max_length");
        builder.Property(question => question.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(question => question.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();

        builder.HasIndex(question => new { question.FormId, question.Order }).IsUnique();
        builder.HasOne<EvaluationForm>()
            .WithMany()
            .HasForeignKey(question => question.FormId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
