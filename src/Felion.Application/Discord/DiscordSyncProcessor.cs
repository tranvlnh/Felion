using System.Text.Json;
using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public sealed class DiscordSyncProcessor(
    IDiscordSyncJobStore store,
    IDiscordRoleGateway roleGateway) : IDiscordSyncProcessor
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
            if (job.Operation != DiscordSyncOperation.SynchronizeRoles)
            {
                throw new DiscordSyncProcessingException("Unsupported Discord sync operation.");
            }

            var mappings = await store.ListRoleMappingsAsync(cancellationToken);
            var managedRoleIds = mappings
                .Select(mapping => mapping.DiscordRoleId)
                .ToHashSet();
            DiscordRoleSyncTarget? target = null;
            long discordUserId;
            IReadOnlyCollection<long> desiredRoleIds;
            if (job.Operation == DiscordSyncOperation.ClearManagedRoles)
            {
                discordUserId = ReadDiscordUserId(job.PayloadJson);
                desiredRoleIds = [];
            }
            else
            {
                target = await store.FindTargetAsync(job, cancellationToken)
                    ?? throw new DiscordSyncProcessingException(
                        "The identity subject for the Discord sync job no longer exists or is not linked.");
                discordUserId = target.DiscordUserId;
                desiredRoleIds = mappings
                    .Where(mapping => IsDesired(mapping, target))
                    .Select(mapping => mapping.DiscordRoleId)
                    .ToHashSet();
            }

            await roleGateway.SynchronizeUserRolesAsync(
                discordUserId,
                desiredRoleIds,
                managedRoleIds,
                cancellationToken);

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

    private static bool IsDesired(DiscordRoleMapping mapping, DiscordRoleSyncTarget target)
    {
        return mapping.Kind switch
        {
            DiscordRoleMappingKind.Position => target.SubjectType == DiscordIdentitySubjectType.Member
                && target.Position?.ToString() == mapping.SubjectKey,
            DiscordRoleMappingKind.Probation => target.SubjectType == DiscordIdentitySubjectType.Probation,
            DiscordRoleMappingKind.Department => target.DepartmentId.ToString("D") == mapping.SubjectKey,
            DiscordRoleMappingKind.Generation => target.GenerationId.ToString("D") == mapping.SubjectKey,
            DiscordRoleMappingKind.ProbationTeam => target.ProbationTeamId?.ToString("D") == mapping.SubjectKey,
            _ => false
        };
    }

    private static string GetSafeError(Exception exception)
    {
        return exception is DiscordRoleGatewayException
            or DiscordSyncProcessingException
            ? exception.Message
            : "Discord synchronization failed.";
    }

    private static long ReadDiscordUserId(string payloadJson)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<DiscordSyncPayload>(payloadJson);
            if (payload?.DiscordUserId is > 0)
            {
                return payload.DiscordUserId.Value;
            }
        }
        catch (JsonException)
        {
            // Convert malformed persisted payloads into a safe processing error below.
        }

        throw new DiscordSyncProcessingException("The Discord sync job payload is invalid.");
    }

    private sealed record DiscordSyncPayload(long? DiscordUserId);
}

public sealed class DiscordSyncProcessingException(string message) : Exception(message);
