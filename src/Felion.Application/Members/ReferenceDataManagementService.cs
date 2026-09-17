using System.Text.Json;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Members;

namespace Felion.Application.Members;

public sealed class DepartmentManagementService(
    IReferenceDataStore store,
    IMemberStore memberStore) : IDepartmentManagementService
{
    public async Task<DepartmentDto> CreateAsync(
        Guid actorMemberId,
        CreateDepartmentCommand command,
        string correlationId,
        CancellationToken cancellationToken,
        long? actorDiscordUserId = null)
    {
        await EnsureAdminAsync(actorMemberId, cancellationToken);

        Department department;
        string slug;
        try
        {
            var name = IdentityNormalizer.RequiredText(command.Name, nameof(command.Name));
            slug = IdentityNormalizer.RequiredText(command.Slug, nameof(command.Slug)).ToLowerInvariant();
            if (name.Length > 100)
            {
                throw new DomainException("Department name must be at most 100 characters.");
            }

            if (slug.Length > 100)
            {
                throw new DomainException("Department slug must be at most 100 characters.");
            }

            department = Department.CreateRegular(name, slug);
        }
        catch (DomainException exception)
        {
            throw new ReferenceDataValidationException(exception.Message);
        }

        if (await store.FindDepartmentBySlugAsync(slug, cancellationToken) is not null)
        {
            throw new ReferenceDataConflictException(
                "A Department with the same slug already exists.");
        }

        var audit = AuditLog.Create(
            actorDiscordUserId is null ? AuditActorType.WebMember : AuditActorType.DiscordMember,
            actorDiscordUserId is null ? actorMemberId : null,
            actorDiscordUserId,
            "DepartmentCreated",
            "Department",
            department.Id,
            correlationId,
            afterJson: Snapshot(department));

        await store.AddDepartmentAsync(department, audit, cancellationToken);
        return ToDto(department);
    }

    private async Task EnsureAdminAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position != MemberPosition.Admin)
        {
            throw new ReferenceDataAccessDeniedException();
        }
    }

    private static DepartmentDto ToDto(Department department)
    {
        return new DepartmentDto(
            department.Id,
            department.Name,
            department.Slug,
            department.IsCore,
            department.IsActive);
    }

    private static string Snapshot(Department department)
    {
        return JsonSerializer.Serialize(new
        {
            department.Id,
            department.Name,
            department.Slug,
            department.IsCore,
            department.IsActive
        });
    }
}

public sealed class GenerationManagementService(
    IReferenceDataStore store,
    IMemberStore memberStore) : IGenerationManagementService
{
    public async Task<GenerationDto> CreateAsync(
        Guid actorMemberId,
        CreateGenerationCommand command,
        string correlationId,
        CancellationToken cancellationToken,
        long? actorDiscordUserId = null)
    {
        await EnsureAdminAsync(actorMemberId, cancellationToken);

        Generation generation;
        string code;
        try
        {
            var name = IdentityNormalizer.RequiredText(command.Name, nameof(command.Name));
            code = IdentityNormalizer.RequiredText(command.Code, nameof(command.Code)).ToUpperInvariant();
            if (name.Length > 100)
            {
                throw new DomainException("Generation name must be at most 100 characters.");
            }

            if (code.Length > 50)
            {
                throw new DomainException("Generation code must be at most 50 characters.");
            }

            generation = Generation.Create(name, code);
        }
        catch (DomainException exception)
        {
            throw new ReferenceDataValidationException(exception.Message);
        }

        if (await store.FindGenerationByCodeAsync(code, cancellationToken) is not null)
        {
            throw new ReferenceDataConflictException(
                "A Generation with the same code already exists.");
        }

        var audit = AuditLog.Create(
            actorDiscordUserId is null ? AuditActorType.WebMember : AuditActorType.DiscordMember,
            actorDiscordUserId is null ? actorMemberId : null,
            actorDiscordUserId,
            "GenerationCreated",
            "Generation",
            generation.Id,
            correlationId,
            afterJson: Snapshot(generation));

        await store.AddGenerationAsync(generation, audit, cancellationToken);
        return ToDto(generation);
    }

    private async Task EnsureAdminAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position != MemberPosition.Admin)
        {
            throw new ReferenceDataAccessDeniedException();
        }
    }

    private static GenerationDto ToDto(Generation generation)
    {
        return new GenerationDto(
            generation.Id,
            generation.Name,
            generation.Code,
            generation.IsActive);
    }

    private static string Snapshot(Generation generation)
    {
        return JsonSerializer.Serialize(new
        {
            generation.Id,
            generation.Name,
            generation.Code,
            generation.IsActive
        });
    }
}
