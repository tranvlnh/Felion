using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felion.Infrastructure.Persistence.Configurations;

public sealed class ProbationCandidateConfiguration : IEntityTypeConfiguration<ProbationCandidate>
{
    public void Configure(EntityTypeBuilder<ProbationCandidate> builder)
    {
        builder.ToTable(
            "probation_candidates",
            table => table.HasCheckConstraint(
                "ck_probation_candidates_status",
                "status IN ('Active', 'Passed', 'Failed', 'Archived')"));

        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Id).HasColumnName("id");
        builder.Property(candidate => candidate.StudentId).HasColumnName("student_id").HasMaxLength(50).IsRequired();
        builder.Property(candidate => candidate.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(candidate => candidate.DepartmentId).HasColumnName("department_id");
        builder.Property(candidate => candidate.GenerationId).HasColumnName("generation_id");
        builder.Property(candidate => candidate.TeamId).HasColumnName("team_id");
        builder.Property(candidate => candidate.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(candidate => candidate.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(candidate => candidate.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();

        builder.HasIndex(candidate => candidate.StudentId).IsUnique();
        builder.HasIndex(candidate => candidate.DepartmentId);
        builder.HasIndex(candidate => candidate.GenerationId);
        builder.HasIndex(candidate => candidate.TeamId);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(candidate => candidate.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Generation>()
            .WithMany()
            .HasForeignKey(candidate => candidate.GenerationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProbationTeam>()
            .WithMany()
            .HasForeignKey(candidate => candidate.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
