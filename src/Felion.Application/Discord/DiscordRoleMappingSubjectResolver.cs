using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Domain.Common;
using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public sealed class DiscordRoleMappingSubjectResolver(
    IMemberStore memberStore,
    IProbationTeamStore probationTeamStore) : IDiscordRoleMappingSubjectResolver
{
    public async Task<string> ResolveAsync(
        DiscordRoleMappingKind kind,
        string subjectName,
        CancellationToken cancellationToken)
    {
        string normalizedName;
        try
        {
            normalizedName = IdentityNormalizer.RequiredText(subjectName, nameof(subjectName));
        }
        catch (DomainException exception)
        {
            throw new DiscordRoleMappingValidationException(exception.Message);
        }

        try
        {
            return kind switch
            {
                DiscordRoleMappingKind.Position or DiscordRoleMappingKind.Probation
                    => DiscordRoleMapping.NormalizeSubjectKey(kind, normalizedName),
                DiscordRoleMappingKind.Department
                    => await ResolveDepartmentAsync(normalizedName, cancellationToken),
                DiscordRoleMappingKind.Generation
                    => await ResolveGenerationAsync(normalizedName, cancellationToken),
                DiscordRoleMappingKind.ProbationTeam
                    => await ResolveProbationTeamAsync(normalizedName, cancellationToken),
                _ => throw new DiscordRoleMappingValidationException(
                    "Unknown Discord role mapping kind.")
            };
        }
        catch (DomainException exception)
        {
            throw new DiscordRoleMappingValidationException(exception.Message);
        }
    }

    private async Task<string> ResolveDepartmentAsync(
        string subjectName,
        CancellationToken cancellationToken)
    {
        var matches = (await memberStore.ListDepartmentsAsync(cancellationToken))
            .Where(department => string.Equals(
                department.Name,
                subjectName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return ResolveUniqueId("Department", subjectName, matches.Select(department => department.Id));
    }

    private async Task<string> ResolveGenerationAsync(
        string subjectName,
        CancellationToken cancellationToken)
    {
        var matches = (await memberStore.ListGenerationsAsync(cancellationToken))
            .Where(generation => string.Equals(
                generation.Name,
                subjectName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return ResolveUniqueId("Generation", subjectName, matches.Select(generation => generation.Id));
    }

    private async Task<string> ResolveProbationTeamAsync(
        string subjectName,
        CancellationToken cancellationToken)
    {
        var matches = (await probationTeamStore.ListAsync(cancellationToken))
            .Where(team => string.Equals(
                team.Name,
                subjectName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return ResolveUniqueId("ProbationTeam", subjectName, matches.Select(team => team.Id));
    }

    private static string ResolveUniqueId(
        string entityName,
        string subjectName,
        IEnumerable<Guid> matchingIds)
    {
        var ids = matchingIds.ToArray();
        if (ids.Length == 0)
        {
            throw new DiscordRoleMappingValidationException(
                $"No {entityName} matched the name '{subjectName}'.");
        }

        if (ids.Length > 1)
        {
            throw new DiscordRoleMappingValidationException(
                $"The {entityName} name '{subjectName}' is ambiguous.");
        }

        return ids[0].ToString("D");
    }
}
