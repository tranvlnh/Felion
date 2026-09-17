using Felion.Domain.Audit;
using Felion.Domain.Members;

namespace Felion.Application.Members;

public sealed record CreateDepartmentCommand(string Name, string Slug);

public sealed record CreateGenerationCommand(string Name, string Code);

public sealed record DepartmentDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsCore,
    bool IsActive);

public sealed record GenerationDto(
    Guid Id,
    string Name,
    string Code,
    bool IsActive);

public interface IReferenceDataStore
{
    public Task<Department?> FindDepartmentBySlugAsync(
        string normalizedSlug,
        CancellationToken cancellationToken);

    public Task<Generation?> FindGenerationByCodeAsync(
        string normalizedCode,
        CancellationToken cancellationToken);

    public Task AddDepartmentAsync(
        Department department,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task AddGenerationAsync(
        Generation generation,
        AuditLog auditLog,
        CancellationToken cancellationToken);
}

public interface IDepartmentManagementService
{
    public Task<DepartmentDto> CreateAsync(
        Guid actorMemberId,
        CreateDepartmentCommand command,
        string correlationId,
        CancellationToken cancellationToken,
        long? actorDiscordUserId = null);
}

public interface IGenerationManagementService
{
    public Task<GenerationDto> CreateAsync(
        Guid actorMemberId,
        CreateGenerationCommand command,
        string correlationId,
        CancellationToken cancellationToken,
        long? actorDiscordUserId = null);
}
