using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public sealed class DiscordSyncProcessor(
    IDiscordSyncJobStore store,
    IDiscordGuildGateway? guildGateway = null) : IDiscordSyncProcessor
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var job = await store.ClaimNextAsync(cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            switch (job.Operation)
            {
                case DiscordSyncOperation.KickUser:
                    if (guildGateway is null)
                    {
                        throw new DiscordSyncProcessingException("Discord guild operations are unavailable.");
                    }

                    await guildGateway.KickUserAsync(
                        ReadDiscordUserId(job.PayloadJson),
                        cancellationToken);
                    break;
                default:
                    throw new DiscordSyncProcessingException("Unsupported Discord sync operation.");
            }

            job.MarkSucceeded();
            await store.SaveJobAsync(job, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            job.MarkFailed(GetSafeError(exception));
            await store.SaveJobAsync(job, cancellationToken);
        }

        return true;
    }

    private static string GetSafeError(Exception exception)
    {
        return exception is DiscordRoleGatewayException
            or DiscordGuildGatewayException
            or DiscordSyncProcessingException
            ? exception.Message
            : "Discord synchronization failed.";
    }

    private static long ReadDiscordUserId(string payloadJson)
    {
        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<DiscordSyncPayload>(payloadJson);
            if (payload?.DiscordUserId is > 0)
            {
                return payload.DiscordUserId.Value;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Convert malformed persisted payloads into a safe processing error below.
        }

        throw new DiscordSyncProcessingException("The Discord sync job payload is invalid.");
    }

    private sealed record DiscordSyncPayload(long? DiscordUserId);
}

public sealed class DiscordSyncProcessingException(string message) : Exception(message);
