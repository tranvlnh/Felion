using Felion.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class MentorEvaluationConfiguration : IEntityTypeConfiguration<MentorEvaluation>
{
    public void Configure(EntityTypeBuilder<MentorEvaluation> builder)
    {
        builder.ToTable(
            "mentor_evaluations",
            table => table.HasCheckConstraint(
                "ck_mentor_evaluations_scores",
                "attendance BETWEEN 1 AND 10 AND task_completion BETWEEN 1 AND 10 AND learning_initiative BETWEEN 1 AND 10"));

        builder.HasKey(evaluation => evaluation.Id);
        builder.Property(evaluation => evaluation.Id).HasColumnName("id");
        builder.Property(evaluation => evaluation.EvaluationPeriodId).HasColumnName("evaluation_period_id").IsRequired();
        builder.Property(evaluation => evaluation.MentorMemberId).HasColumnName("mentor_member_id").IsRequired();
        builder.Property(evaluation => evaluation.TargetCandidateId).HasColumnName("target_candidate_id").IsRequired();
        builder.Property(evaluation => evaluation.MentorStudentIdSnapshot).HasColumnName("mentor_student_id_snapshot").HasMaxLength(50).IsRequired();
        builder.Property(evaluation => evaluation.MentorNameSnapshot).HasColumnName("mentor_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(evaluation => evaluation.TargetStudentIdSnapshot).HasColumnName("target_student_id_snapshot").HasMaxLength(50).IsRequired();
        builder.Property(evaluation => evaluation.TargetNameSnapshot).HasColumnName("target_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(evaluation => evaluation.Attendance).HasColumnName("attendance").IsRequired();
        builder.Property(evaluation => evaluation.TaskCompletion).HasColumnName("task_completion").IsRequired();
        builder.Property(evaluation => evaluation.LearningInitiative).HasColumnName("learning_initiative").IsRequired();
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
            evaluation.MentorMemberId,
            evaluation.TargetCandidateId
        }).IsUnique();
        builder.HasIndex(evaluation => new { evaluation.EvaluationPeriodId, evaluation.TargetCandidateId });
    }
}
