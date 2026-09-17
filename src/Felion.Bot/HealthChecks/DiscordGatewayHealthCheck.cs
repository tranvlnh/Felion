using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetCord.Gateway;

namespace Felion.Bot.HealthChecks;

/// <summary>
/// Reports whether the Discord Gateway has completed its connection handshake.
/// </summary>
public sealed class DiscordGatewayHealthCheck(GatewayClient gatewayClient) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = gatewayClient.Status;
        var result = status == WebSocketStatus.Ready
            ? HealthCheckResult.Healthy("Discord Gateway is ready.")
            : HealthCheckResult.Unhealthy($"Discord Gateway status is {status}.");

        return Task.FromResult(result);
    }
}
