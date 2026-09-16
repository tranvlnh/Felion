using Felion.Domain.Audit;
using Felion.Domain.Members;

namespace Felion.Application.Members;

public sealed record CreateMemberCommand(
    string StudentId,
    string FullName,
    string ClubEmail,
    Guid DepartmentId,
    Guid GenerationId,
    MemberPosition Position);

public sealed record UpdateMemberCommand(
    string? StudentId = null,
    string? FullName = null,
    string? ClubEmail = null,
    Guid? DepartmentId = null,
    Guid? GenerationId = null,
    MemberPosition? Position = null,
    MemberStatus? Status = null);

public sealed record ListMembersQuery(int Page = 1, int PageSize = 50, MemberStatus? Status = null);

public sealed record MemberDto(
    Guid Id,
    string StudentId,
    string FullName,
    string ClubEmail,
    Guid DepartmentId,
    Guid GenerationId,
    MemberPosition Position,
    MemberStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MemberPage(
    IReadOnlyList<MemberDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record MemberImportRow(
    int RowNumber,
    string StudentId,
    string FullName,
    string ClubEmail,
    string Department,
    string Generation,
    string Position);

public sealed record MemberImportError(int RowNumber, string Field, string Message);

public sealed record MemberImportReadResult(
    IReadOnlyList<MemberImportRow> Rows,
    IReadOnlyList<MemberImportError> Errors);

public sealed record MemberImportReport(
    string FileName,
    int TotalRows,
    int ImportedRows,
    bool Committed,
    IReadOnlyList<MemberImportError> Errors);

public sealed record MemberAuditEntry(Member Member, AuditLog AuditLog);

public sealed record MemberIdentityLookup(
    IReadOnlySet<string> StudentIds,
    IReadOnlySet<string> ClubEmails);

public interface IMemberStore
{
    public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken);

    public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        MemberStatus? status,
        CancellationToken cancellationToken);

    public Task<MemberIdentityLookup> FindIdentityConflictsAsync(
        IReadOnlyCollection<string> studentIds,
        IReadOnlyCollection<string> clubEmails,
        CancellationToken cancellationToken);

    public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken);

    public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken);

    public Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken);

    public Task AddAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken);

    public Task UpdateAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken);

    public Task AddRangeAsync(
        IReadOnlyCollection<MemberAuditEntry> entries,
        CancellationToken cancellationToken);
}

public interface IMemberImportReader
{
    public Task<MemberImportReadResult> ReadAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken);
}

public interface IMemberManagementService
{
    public Task<MemberDto> CreateAsync(
        Guid actorMemberId,
        CreateMemberCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<MemberPage> ListAsync(
        Guid actorMemberId,
        ListMembersQuery query,
        CancellationToken cancellationToken);

    public Task<MemberDto> GetAsync(
        Guid actorMemberId,
        Guid memberId,
        CancellationToken cancellationToken);

    public Task<MemberDto> UpdateAsync(
        Guid actorMemberId,
        Guid memberId,
        UpdateMemberCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<MemberImportReport> ImportAsync(
        Guid actorMemberId,
        string fileName,
        MemberImportReadResult readResult,
        string correlationId,
        CancellationToken cancellationToken);
}
