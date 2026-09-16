using System.Globalization;
using Felion.Application.Probation;
using Microsoft.Extensions.Configuration;

namespace Felion.Infrastructure.Persistence;

internal sealed class EvaluationDefaultsProvider(IConfiguration configuration) : IEvaluationDefaultsProvider
{
    public EvaluationDefaults GetDefaults()
    {
        return new EvaluationDefaults(
            ReadDecimal("Evaluation:DefaultScoreMin", 1),
            ReadDecimal("Evaluation:DefaultScoreMax", 5),
            ReadInt("Evaluation:DefaultTextMaxLength", 2000));
    }

    private decimal ReadDecimal(string key, decimal fallback)
    {
        return decimal.TryParse(
            configuration[key],
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }

    private int ReadInt(string key, int fallback)
    {
        return int.TryParse(configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
