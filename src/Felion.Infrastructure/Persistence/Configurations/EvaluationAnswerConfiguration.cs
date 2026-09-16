using Felion.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class EvaluationAnswerConfiguration : IEntityTypeConfiguration<EvaluationAnswer>
{
    public void Configure(EntityTypeBuilder<EvaluationAnswer> builder)
    {
        builder.ToTable(
            "evaluation_answers",
            table => table.HasCheckConstraint(
                "ck_evaluation_answers_value",
                "(question_type_snapshot = 'Score' AND text_value IS NULL) OR "
                + "(question_type_snapshot = 'Text' AND score_value IS NULL)"));

        builder.HasKey(answer => answer.Id);
        builder.Property(answer => answer.Id).HasColumnName("id");
        builder.Property(answer => answer.SubmissionId).HasColumnName("submission_id").IsRequired();
        builder.Property(answer => answer.QuestionId).HasColumnName("question_id");
        builder.Property(answer => answer.QuestionPromptSnapshot).HasColumnName("question_prompt_snapshot").HasMaxLength(2000).IsRequired();
        builder.Property(answer => answer.QuestionTypeSnapshot).HasColumnName("question_type_snapshot").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(answer => answer.ScoreValue).HasColumnName("score_value").HasPrecision(18, 4);
        builder.Property(answer => answer.TextValue).HasColumnName("text_value").HasColumnType("text");
        builder.Property(answer => answer.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(answer => answer.SubmissionId);
        builder.HasIndex(answer => answer.QuestionId);
        builder.HasOne<EvaluationSubmission>()
            .WithMany()
            .HasForeignKey(answer => answer.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EvaluationQuestion>()
            .WithMany()
            .HasForeignKey(answer => answer.QuestionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
