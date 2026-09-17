using Felion.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class PeerEvaluationConfiguration : IEntityTypeConfiguration<PeerEvaluation>
{
    public void Configure(EntityTypeBuilder<PeerEvaluation> builder)
    {
        builder.ToTable(
            "peer_evaluations",
            table => table.HasCheckConstraint(
                "ck_peer_evaluations_scores",
                "contribution BETWEEN 1 AND 5 AND communication BETWEEN 1 AND 5 AND attitude BETWEEN 1 AND 5"));

        builder.HasKey(evaluation => evaluation.Id);
        builder.Property(evaluation => evaluation.Id).HasColumnName("id");
        builder.Property(evaluation => evaluation.EvaluationPeriodId).HasColumnName("evaluation_period_id").IsRequired();
        builder.Property(evaluation => evaluation.EvaluatorCandidateId).HasColumnName("evaluator_candidate_id").IsRequired();
        builder.Property(evaluation => evaluation.TargetCandidateId).HasColumnName("target_candidate_id").IsRequired();
        builder.Property(evaluation => evaluation.EvaluatorStudentIdSnapshot).HasColumnName("evaluator_student_id_snapshot").HasMaxLength(50).IsRequired();
        builder.Property(evaluation => evaluation.EvaluatorNameSnapshot).HasColumnName("evaluator_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(evaluation => evaluation.TargetStudentIdSnapshot).HasColumnName("target_student_id_snapshot").HasMaxLength(50).IsRequired();
        builder.Property(evaluation => evaluation.TargetNameSnapshot).HasColumnName("target_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(evaluation => evaluation.Contribution).HasColumnName("contribution").IsRequired();
        builder.Property(evaluation => evaluation.Communication).HasColumnName("communication").IsRequired();
        builder.Property(evaluation => evaluation.Attitude).HasColumnName("attitude").IsRequired();
        builder.Property(evaluation => evaluation.Note).HasColumnName("note").HasColumnType("text");
        builder.Property(evaluation => evaluation.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(evaluation => evaluation.UpdatedAt).HasColumnName("updated_at").IsConcurrencyToken();

        builder.HasOne<EvaluationPeriod>()
            .WithMany()
            .HasForeignKey(evaluation => evaluation.EvaluationPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(evaluation => new
        {
            evaluation.EvaluationPeriodId,
            evaluation.EvaluatorCandidateId,
            evaluation.TargetCandidateId
        }).IsUnique();
        builder.HasIndex(evaluation => new { evaluation.EvaluationPeriodId, evaluation.TargetCandidateId });
    }
}
