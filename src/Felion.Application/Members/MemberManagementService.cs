using System.Text.Json;
using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Members;

public sealed class MemberManagementService(
    IMemberStore store,
    IDiscordMemberSyncStore discordMemberSyncStore,
    IDiscordRoleSynchronizationService? roleSynchronizationService = null) : IMemberManagementService
{
    private const int MaxStudentIdLength = 50;
    private const int MaxFullNameLength = 200;
    private const int MaxClubEmailLength = 320;

    public async Task<MemberDto> CreateAsync(
        Guid actorMemberId,
        CreateMemberCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await GetAuthorizedActorAsync(actorMemberId, cancellationToken);
        var department = await GetActiveDepartmentAsync(command.DepartmentId, cancellationToken);
        var generation = await GetActiveGenerationAsync(command.GenerationId, cancellationToken);

        ValidateLengths(command.StudentId, command.FullName, command.ClubEmail);

        Member member;
        try
        {
            member = Member.Create(
                command.StudentId,
                command.FullName,
                command.ClubEmail,
                department.Id,
                department.IsCore,
                generation.Id,
                command.Position);
        }
        catch (DomainException exception)
        {
            throw new MemberValidationException(exception.Message);
        }

        var audit = CreateAudit(
            actorMemberId,
            "MemberCreated",
            member,
            correlationId,
            after: Snapshot(member));

        await store.AddAsync(member, audit, cancellationToken);
        return ToDto(member);
    }

    public async Task<MemberPage> ListAsync(
        Guid actorMemberId,
        ListMembersQuery query,
        CancellationToken cancellationToken)
    {
        await GetAuthorizedActorAsync(actorMemberId, cancellationToken);

        if (query.Page < 1 || query.PageSize is < 1 or > 200)
        {
            throw new MemberValidationException("Page must be positive and PageSize must be between 1 and 200.");
        }

        var result = await store.ListAsync(query.Page, query.PageSize, query.Status, cancellationToken);
        return new MemberPage(
            result.Items.Select(ToDto).ToArray(),
            query.Page,
            query.PageSize,
            result.TotalCount);
    }

    public async Task<MemberDto> GetAsync(
        Guid actorMemberId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await GetAuthorizedActorAsync(actorMemberId, cancellationToken);
        var member = await store.FindByIdAsync(memberId, track: false, cancellationToken)
            ?? throw new MemberNotFoundException(memberId);

        return ToDto(member);
    }

    public async Task<MemberDto> UpdateAsync(
        Guid actorMemberId,
        Guid memberId,
        UpdateMemberCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await GetAuthorizedActorAsync(actorMemberId, cancellationToken);
        var member = await store.FindByIdAsync(memberId, track: true, cancellationToken)
            ?? throw new MemberNotFoundException(memberId);
        var discordUserId = await discordMemberSyncStore.FindDiscordUserIdAsync(
            memberId,
            cancellationToken);

        var department = await GetActiveDepartmentAsync(command.DepartmentId ?? member.DepartmentId, cancellationToken);
        var generation = await GetActiveGenerationAsync(command.GenerationId ?? member.GenerationId, cancellationToken);
        var studentId = command.StudentId ?? member.StudentId;
        var fullName = command.FullName ?? member.FullName;
        var clubEmail = command.ClubEmail ?? member.ClubEmail;
        var position = command.Position ?? member.Position;
        ValidateLengths(studentId, fullName, clubEmail);

        var before = Snapshot(member);
        try
        {
            member.UpdateProfile(
                studentId,
                fullName,
                clubEmail,
                department.Id,
                department.IsCore,
                generation.Id,
                position);

            if (command.Status is not null)
            {
                member.ChangeStatus(command.Status.Value);
            }
        }
        catch (DomainException exception)
        {
            throw new MemberValidationException(exception.Message);
        }

        var audit = CreateAudit(
            actorMemberId,
            "MemberUpdated",
            member,
            correlationId,
            before,
            Snapshot(member));

        await discordMemberSyncStore.UpdateMemberAsync(member, audit, cancellationToken);
        if (discordUserId is > 0 && roleSynchronizationService is not null)
        {
            await roleSynchronizationService.SynchronizeSubjectAsync(
                DiscordIdentitySubjectType.Member,
                member.Id,
                cancellationToken);
        }

        return ToDto(member);
    }

    public async Task<MemberImportReport> ImportAsync(
        Guid actorMemberId,
        string fileName,
        MemberImportReadResult readResult,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await GetAuthorizedActorAsync(actorMemberId, cancellationToken);

        var errors = readResult.Errors.ToList();
        var normalizedRows = new List<NormalizedImportRow>();
        var departments = await store.ListDepartmentsAsync(cancellationToken);
        var generations = await store.ListGenerationsAsync(cancellationToken);
        var studentIds = new HashSet<string>(StringComparer.Ordinal);
        var clubEmails = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in readResult.Rows)
        {
            var studentId = TryNormalize(row.RowNumber, nameof(MemberImportRow.StudentId), row.StudentId, errors, MaxStudentIdLength);
            var fullName = TryNormalize(row.RowNumber, nameof(MemberImportRow.FullName), row.FullName, errors, MaxFullNameLength);
            var clubEmail = TryNormalize(row.RowNumber, nameof(MemberImportRow.ClubEmail), row.ClubEmail, errors, MaxClubEmailLength, normalizeEmail: true);
            var department = ResolveDepartment(row, departments, errors);
            var generation = ResolveGeneration(row, generations, errors);
            var position = ParsePosition(row, errors);

            if (studentId is not null && !studentIds.Add(studentId))
            {
                errors.Add(new MemberImportError(row.RowNumber, nameof(MemberImportRow.StudentId), "StudentId is duplicated in the import file."));
            }

            if (clubEmail is not null && !clubEmails.Add(clubEmail))
            {
                errors.Add(new MemberImportError(row.RowNumber, nameof(MemberImportRow.ClubEmail), "ClubEmail is duplicated in the import file."));
            }

            if (studentId is not null
                && fullName is not null
                && clubEmail is not null
                && department is not null
                && generation is not null
                && position is not null)
            {
                normalizedRows.Add(new NormalizedImportRow(row, studentId, fullName, clubEmail, department, generation, position.Value));
            }
        }

        var existing = await store.FindIdentityConflictsAsync(studentIds, clubEmails, cancellationToken);
        foreach (var row in normalizedRows)
        {
            if (existing.StudentIds.Contains(row.StudentId))
            {
                errors.Add(new MemberImportError(row.Source.RowNumber, nameof(MemberImportRow.StudentId), "StudentId already exists."));
            }

            if (existing.ClubEmails.Contains(row.ClubEmail))
            {
                errors.Add(new MemberImportError(row.Source.RowNumber, nameof(MemberImportRow.ClubEmail), "ClubEmail already exists."));
            }
        }

        if (errors.Count > 0)
        {
            return new MemberImportReport(fileName, readResult.Rows.Count, 0, false, errors);
        }

        var entries = new List<MemberAuditEntry>();
        foreach (var row in normalizedRows)
        {
            try
            {
                var member = Member.Create(
                    row.StudentId,
                    row.FullName,
                    row.ClubEmail,
                    row.Department.Id,
                    row.Department.IsCore,
                    row.Generation.Id,
                    row.Position);
                var audit = CreateAudit(
                    actorMemberId,
                    "MemberImported",
                    member,
                    correlationId,
                    after: JsonSerializer.Serialize(new
                    {
                        source = fileName,
                        row = row.Source.RowNumber,
                        member = Snapshot(member)
                    }));
                entries.Add(new MemberAuditEntry(member, audit));
            }
            catch (DomainException exception)
            {
                errors.Add(new MemberImportError(row.Source.RowNumber, "Member", exception.Message));
            }
        }

        if (errors.Count > 0)
        {
            return new MemberImportReport(fileName, readResult.Rows.Count, 0, false, errors);
        }

        try
        {
            await store.AddRangeAsync(entries, cancellationToken);
        }
        catch (MemberConflictException exception)
        {
            return new MemberImportReport(
                fileName,
                readResult.Rows.Count,
                0,
                false,
                [new MemberImportError(0, "File", exception.Message)]);
        }

        return new MemberImportReport(fileName, readResult.Rows.Count, entries.Count, true, []);
    }

    private async Task<Member> GetAuthorizedActorAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await store.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position is not (MemberPosition.Admin or MemberPosition.Core))
        {
            throw new MemberAccessDeniedException();
        }

        return actor;
    }

    private async Task<Department> GetActiveDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await store.FindDepartmentAsync(departmentId, cancellationToken);
        if (department is null || !department.IsActive)
        {
            throw new MemberValidationException("Department must exist and be active.");
        }

        return department;
    }

    private async Task<Generation> GetActiveGenerationAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var generation = await store.FindGenerationAsync(generationId, cancellationToken);
        if (generation is null || !generation.IsActive)
        {
            throw new MemberValidationException("Generation must exist and be active.");
        }

        return generation;
    }

    private static void ValidateLengths(string studentId, string fullName, string clubEmail)
    {
        if (studentId.Trim().Length > MaxStudentIdLength)
        {
            throw new MemberValidationException($"StudentId must be at most {MaxStudentIdLength} characters.");
        }

        if (fullName.Trim().Length > MaxFullNameLength)
        {
            throw new MemberValidationException($"FullName must be at most {MaxFullNameLength} characters.");
        }

        if (clubEmail.Trim().Length > MaxClubEmailLength)
        {
            throw new MemberValidationException($"ClubEmail must be at most {MaxClubEmailLength} characters.");
        }
    }

    private static string? TryNormalize(
        int rowNumber,
        string field,
        string value,
        List<MemberImportError> errors,
        int maxLength,
        bool normalizeEmail = false)
    {
        try
        {
            var normalized = normalizeEmail ? IdentityNormalizer.ClubEmail(value) : field == nameof(MemberImportRow.StudentId)
                ? IdentityNormalizer.StudentId(value)
                : IdentityNormalizer.RequiredText(value, field);
            if (normalized.Length > maxLength)
            {
                errors.Add(new MemberImportError(rowNumber, field, $"{field} must be at most {maxLength} characters."));
                return null;
            }

            return normalized;
        }
        catch (DomainException exception)
        {
            errors.Add(new MemberImportError(rowNumber, field, exception.Message));
            return null;
        }
    }

    private static Department? ResolveDepartment(
        MemberImportRow row,
        IReadOnlyList<Department> departments,
        List<MemberImportError> errors)
    {
        var value = row.Department.Trim();
        var matches = departments.Where(department =>
                string.Equals(department.Id.ToString(), value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(department.Slug, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(department.Name, value, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var activeMatches = matches.Where(department => department.IsActive).ToArray();
        if (activeMatches.Length == 1)
        {
            return activeMatches[0];
        }

        errors.Add(new MemberImportError(
            row.RowNumber,
            nameof(MemberImportRow.Department),
            activeMatches.Length == 0 ? "Department does not exist or is inactive." : "Department value is ambiguous."));
        return null;
    }

    private static Generation? ResolveGeneration(
        MemberImportRow row,
        IReadOnlyList<Generation> generations,
        List<MemberImportError> errors)
    {
        var value = row.Generation.Trim();
        var matches = generations.Where(generation =>
                string.Equals(generation.Id.ToString(), value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(generation.Code, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(generation.Name, value, StringComparison.OrdinalIgnoreCase))
            .Where(generation => generation.IsActive)
            .ToArray();
        if (matches.Length == 1)
        {
            return matches[0];
        }

        errors.Add(new MemberImportError(
            row.RowNumber,
            nameof(MemberImportRow.Generation),
            matches.Length == 0 ? "Generation does not exist or is inactive." : "Generation value is ambiguous."));
        return null;
    }

    private static MemberPosition? ParsePosition(
        MemberImportRow row,
        List<MemberImportError> errors)
    {
        if (Enum.TryParse<MemberPosition>(row.Position.Trim(), ignoreCase: true, out var position))
        {
            return position;
        }

        errors.Add(new MemberImportError(
            row.RowNumber,
            nameof(MemberImportRow.Position),
            "Position must be Admin, Core or Member."));
        return null;
    }

    private static AuditLog CreateAudit(
        Guid actorMemberId,
        string action,
        Member member,
        string correlationId,
        string? before = null,
        string? after = null)
    {
        return AuditLog.Create(
            AuditActorType.WebMember,
            actorMemberId,
            null,
            action,
            "Member",
            member.Id,
            correlationId,
            afterJson: after,
            beforeJson: before);
    }

    private static MemberDto ToDto(Member member)
    {
        return new MemberDto(
            member.Id,
            member.StudentId,
            member.FullName,
            member.ClubEmail,
            member.DepartmentId,
            member.GenerationId,
            member.Position,
            member.Status,
            member.CreatedAt,
            member.UpdatedAt);
    }

    private static string Snapshot(Member member)
    {
        return JsonSerializer.Serialize(new
        {
            member.Id,
            member.StudentId,
            member.FullName,
            member.ClubEmail,
            member.DepartmentId,
            member.GenerationId,
            member.Position,
            member.Status,
            member.CreatedAt,
            member.UpdatedAt
        });
    }

    private sealed record NormalizedImportRow(
        MemberImportRow Source,
        string StudentId,
        string FullName,
        string ClubEmail,
        Department Department,
        Generation Generation,
        MemberPosition Position);
}
