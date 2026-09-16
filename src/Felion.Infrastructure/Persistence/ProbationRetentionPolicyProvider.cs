using Felion.Application.Probation;
using Microsoft.Extensions.Configuration;

namespace Felion.Infrastructure.Persistence;

internal sealed class ProbationRetentionPolicyProvider(IConfiguration configuration)
    : IProbationRetentionPolicyProvider
{
    public ProbationRetentionPolicy GetPolicy()
    {
        return new ProbationRetentionPolicy(
            ReadEnum("Probation:SuccessPolicy", ProbationSuccessPolicy.Archive),
            ReadEnum("Probation:FailurePolicy", ProbationFailurePolicy.MarkInactive));
    }

    private T ReadEnum<T>(string key, T fallback)
        where T : struct, Enum
    {
        return Enum.TryParse<T>(configuration[key], ignoreCase: true, out var value)
            && Enum.IsDefined(value)
            ? value
            : fallback;
    }
}
