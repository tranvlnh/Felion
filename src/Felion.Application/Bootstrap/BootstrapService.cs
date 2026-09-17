using System.Text.Json;
using Felion.Domain.Audit;
using Felion.Domain.Members;

namespace Felion.Application.Bootstrap;

public sealed class BootstrapService(IBootstrapStore store)
{
    public async Task<BootstrapResult> BootstrapAdminAsync(
        BootstrapAdminCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.StudentId)
            || string.IsNullOrWhiteSpace(command.FullName)
            || string.IsNullOrWhiteSpace(command.ClubEmail)
            || string.IsNullOrWhiteSpace(command.GenerationName)
            || string.IsNullOrWhiteSpace(command.GenerationCode))
        {
            throw new ArgumentException(
                "Bootstrap configuration requires StudentId, FullName, ClubEmail, GenerationName, and GenerationCode.",
                nameof(command));
        }

        if (await store.HasAnyMembersAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Database already contains one or more members. Bootstrap is only permitted on an empty database.");
        }

        var coreDepartment = await store.FindDepartmentByIdAsync(Department.CoreDepartmentId, cancellationToken);
        if (coreDepartment is null)
        {
            throw new InvalidOperationException(
                $"Core department '{Department.CoreDepartmentId}' was not found. Ensure database migrations have been applied.");
        }

        var normalizedGenCode = command.GenerationCode.Trim().ToUpperInvariant();
        var existingGeneration = await store.FindGenerationByCodeAsync(normalizedGenCode, cancellationToken);

        Generation generation;
        Generation? newGeneration = null;

        if (existingGeneration is not null)
        {
            generation = existingGeneration;
        }
        else
        {
            generation = Generation.Create(command.GenerationName, normalizedGenCode);
            newGeneration = generation;
        }

        var adminMember = Member.Create(
            command.StudentId,
            command.FullName,
            command.ClubEmail,
            coreDepartment.Id,
            isCoreDepartment: true,
            generation.Id,
            MemberPosition.Admin);

        var correlationId = $"bootstrap-{Guid.NewGuid():N}";
        var auditLogs = new List<AuditLog>();

        if (newGeneration is not null)
        {
            auditLogs.Add(AuditLog.Create(
                AuditActorType.System,
                actorMemberId: null,
                actorDiscordUserId: null,
                action: "BootstrapGenerationCreated",
                entityType: "Generation",
                entityId: newGeneration.Id,
                correlationId: correlationId,
                metadataJson: JsonSerializer.Serialize(new
                {
                    newGeneration.Name,
                    newGeneration.Code
                })));
        }

        auditLogs.Add(AuditLog.Create(
            AuditActorType.System,
            actorMemberId: null,
            actorDiscordUserId: null,
            action: "BootstrapAdminCreated",
            entityType: "Member",
            entityId: adminMember.Id,
            correlationId: correlationId,
            metadataJson: JsonSerializer.Serialize(new
            {
                adminMember.StudentId,
                adminMember.ClubEmail,
                Position = adminMember.Position.ToString()
            }),
            afterJson: JsonSerializer.Serialize(new
            {
                adminMember.Id,
                adminMember.StudentId,
                adminMember.FullName,
                adminMember.ClubEmail,
                adminMember.DepartmentId,
                adminMember.GenerationId,
                Position = adminMember.Position.ToString(),
                Status = adminMember.Status.ToString()
            })));

        await store.SaveBootstrapAsync(newGeneration, adminMember, auditLogs, cancellationToken);

        return new BootstrapResult(
            adminMember.Id,
            adminMember.ClubEmail,
            generation.Name,
            generation.Id);
    }
}
