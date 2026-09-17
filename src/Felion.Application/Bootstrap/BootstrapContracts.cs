using Felion.Domain.Audit;
using Felion.Domain.Members;

namespace Felion.Application.Bootstrap;

public sealed record BootstrapAdminCommand(
    string StudentId,
    string FullName,
    string ClubEmail,
    string GenerationName,
    string GenerationCode);

public sealed record BootstrapResult(
    Guid MemberId,
    string ClubEmail,
    string GenerationName,
    Guid GenerationId);

public interface IBootstrapStore
{
    public Task<bool> HasAnyMembersAsync(CancellationToken cancellationToken);

    public Task<Department?> FindDepartmentByIdAsync(Guid departmentId, CancellationToken cancellationToken);

    public Task<Generation?> FindGenerationByCodeAsync(string code, CancellationToken cancellationToken);

    public Task SaveBootstrapAsync(
        Generation? newGeneration,
        Member adminMember,
        IReadOnlyCollection<AuditLog> auditLogs,
        CancellationToken cancellationToken);
}
