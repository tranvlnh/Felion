using Felion.Domain.Evaluation;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class EvaluationSubmissionConfiguration : IEntityTypeConfiguration<EvaluationSubmission>
{
    public void Configure(EntityTypeBuilder<EvaluationSubmission> builder)
    {
        builder.ToTable(
            "evaluation_submissions",
            table => table.HasCheckConstraint(
                "ck_evaluation_submissions_reviewer",
                "(reviewer_type = 'Peer' AND reviewer_candidate_id IS NOT NULL AND reviewer_member_id IS NULL) OR "
                + "(reviewer_type = 'Mentor' AND reviewer_member_id IS NOT NULL AND reviewer_candidate_id IS NULL)"));

        builder.HasKey(submission => submission.Id);
        builder.Property(submission => submission.Id).HasColumnName("id");
        builder.Property(submission => submission.FormId).HasColumnName("form_id").IsRequired();
        builder.Property(submission => submission.PeriodId).HasColumnName("period_id").IsRequired();
        builder.Property(submission => submission.ReviewerType).HasColumnName("reviewer_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(submission => submission.ReviewerMemberId).HasColumnName("reviewer_member_id");
        builder.Property(submission => submission.ReviewerCandidateId).HasColumnName("reviewer_candidate_id");
        builder.Property(submission => submission.ReviewerStudentIdSnapshot).HasColumnName("reviewer_student_id_snapshot").HasMaxLength(50).IsRequired();
        builder.Property(submission => submission.ReviewerNameSnapshot).HasColumnName("reviewer_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(submission => submission.TargetCandidateId).HasColumnName("target_candidate_id");
        builder.Property(submission => submission.TargetStudentIdSnapshot).HasColumnName("target_student_id_snapshot").HasMaxLength(50).IsRequired();
        builder.Property(submission => submission.TargetNameSnapshot).HasColumnName("target_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(submission => submission.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        builder.Property(submission => submission.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();

        builder.HasIndex(submission => new
        {
            submission.FormId,
            submission.ReviewerCandidateId,
            submission.TargetCandidateId
        }).IsUnique().HasFilter("reviewer_candidate_id IS NOT NULL AND target_candidate_id IS NOT NULL");
        builder.HasIndex(submission => new
        {
            submission.FormId,
            submission.ReviewerMemberId,
            submission.TargetCandidateId
        }).IsUnique().HasFilter("reviewer_member_id IS NOT NULL AND target_candidate_id IS NOT NULL");
        builder.HasIndex(submission => new { submission.PeriodId, submission.FormId });
        builder.HasIndex(submission => submission.TargetCandidateId);

        builder.HasOne<EvaluationForm>()
            .WithMany()
            .HasForeignKey(submission => submission.FormId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvaluationPeriod>()
            .WithMany()
            .HasForeignKey(submission => submission.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(submission => submission.ReviewerMemberId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ProbationCandidate>()
            .WithMany()
            .HasForeignKey(submission => submission.ReviewerCandidateId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ProbationCandidate>()
            .WithMany()
            .HasForeignKey(submission => submission.TargetCandidateId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
