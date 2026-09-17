using Felion.Application.Discord;
using Felion.Application.Identity;
using Felion.Application.Probation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Felion.Api;

internal static class ProbationDecisionsApi
{
    public static void Map(IEndpointRouteBuilder group)
    {
        var decisions = group
            .MapGroup("/probation/decisions")
            .WithTags("Probation")
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        decisions.MapPost("", DecideAsync);

        static async Task<IResult> DecideAsync(
            DecideProbationRequest request,
            HttpContext httpContext,
            IProbationDecisionService service,
            CancellationToken cancellationToken = default)
        {
            if (!WebIdentityPrincipal.TryGetMemberId(httpContext.User, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            if (request.Items is null || request.Items.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Items)] = ["At least one probation decision is required."]
                });
            }

            var parsed = new List<ProbationDecisionRequest>(request.Items.Count);
            for (var index = 0; index < request.Items.Count; index++)
            {
                var item = request.Items[index];
                if (!Enum.TryParse<ProbationDecision>(item.Decision, ignoreCase: true, out var decision)
                    || !Enum.IsDefined(decision))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [$"Items[{index}].Decision"] = ["Decision must be Pass or Fail."]
                    });
                }

                parsed.Add(new ProbationDecisionRequest(item.CandidateId, decision));
            }

            try
            {
                return Results.Ok(await service.DecideAsync(
                    actorMemberId,
                    parsed,
                    GetCorrelationId(httpContext),
                    cancellationToken));
            }
            catch (ProbationDecisionAccessDeniedException exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: exception.Message);
            }
            catch (ProbationDecisionValidationException exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: exception.Message);
            }
            catch (DiscordRoleGatewayException exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status502BadGateway, detail: exception.Message);
            }
        }
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        return httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
    }

    public sealed record DecideProbationRequest(IReadOnlyList<DecisionRequest>? Items);

    public sealed record DecisionRequest(Guid CandidateId, string Decision);
}
