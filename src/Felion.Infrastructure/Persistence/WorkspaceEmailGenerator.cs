using System.Globalization;
using System.Text;
using Felion.Application.Probation;
using Felion.Domain.Common;
using Microsoft.Extensions.Configuration;

namespace Felion.Infrastructure.Persistence;

public sealed class WorkspaceEmailGenerator(IConfiguration configuration)
    : IWorkspaceEmailGenerator
{
    public string Generate(string fullName)
    {
        var normalizedName = IdentityNormalizer.RequiredText(fullName, nameof(fullName));
        var parts = RemoveDiacritics(normalizedName)
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            throw new InvalidOperationException("A Workspace email cannot be generated without a name.");
        }

        var localPart = parts[^1] + string.Concat(parts[..^1].Select(part => part[0]));
        var workspaceDomain = IdentityNormalizer.RequiredText(
            configuration["Authentication:Google:WorkspaceDomain"] ?? string.Empty,
            "WorkspaceDomain")
            .TrimStart('@')
            .ToLowerInvariant();
        return IdentityNormalizer.ClubEmail($"{localPart}@{workspaceDomain}");
    }

    private static string RemoveDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
