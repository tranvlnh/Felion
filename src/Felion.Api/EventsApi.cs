using Felion.Application.Events;
using Felion.Application.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Felion.Api;

internal static class EventsApi
{
    public static void Map(IEndpointRouteBuilder group)
    {
        var events = group
            .MapGroup("/events")
            .WithTags("Events")
            .RequireAuthorization(FelionAuthorizationPolicies.ActiveMember);

        events.MapGet("", ListAsync);
        events.MapGet("/{eventId:guid}", GetAsync);
        events.MapPost("", CreateAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPatch("/{eventId:guid}", UpdateAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/publish", PublishAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/registration/close", CloseRegistrationAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/start", StartAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/complete", CompleteAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/cancel", CancelAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/positions", AddPositionAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPatch("/{eventId:guid}/positions/{positionId:guid}", UpdatePositionAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/positions/{positionId:guid}/registrations", RegisterAsync);
        events.MapGet("/{eventId:guid}/registrations", ListRegistrationsAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/registrations/{registrationId:guid}/approve", ApproveRegistrationAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/registrations/{registrationId:guid}/reject", RejectRegistrationAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/positions/{positionId:guid}/assignments", AssignAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapPost("/{eventId:guid}/attendance", CheckInAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        events.MapGet("/{eventId:guid}/attendance", ListAttendanceAsync).RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);

        var members = group
            .MapGroup("/members")
            .WithTags("Events")
            .RequireAuthorization(FelionAuthorizationPolicies.ActiveMember);
        members.MapGet("/{memberId:guid}/event-history", ListMemberHistoryAsync);

        async Task<IResult> ListAsync(
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                httpContext,
                () => service.ListAsync(GetActor(httpContext), cancellationToken));
        }

        async Task<IResult> GetAsync(
            Guid eventId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                httpContext,
                () => service.GetAsync(GetActor(httpContext), eventId, cancellationToken));
        }

        async Task<IResult> CreateAsync(
            CreateEventRequest request,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var @event = await service.CreateAsync(
                    GetActor(httpContext),
                    new CreateEventCommand(
                        request.Name,
                        request.Description,
                        request.Location,
                        request.StartsAt,
                        request.EndsAt,
                        request.AllowMultiplePositions),
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Created($"/api/v1/events/{@event.Id}", @event);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                or EventAccessDeniedException
                or EventValidationException
                or EventConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> UpdateAsync(
            Guid eventId,
            UpdateEventRequest request,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.UpdateAsync(
                    GetActor(httpContext),
                    eventId,
                    new UpdateEventCommand(
                        request.Name,
                        request.Description,
                        request.Location,
                        request.StartsAt,
                        request.EndsAt,
                        request.AllowMultiplePositions),
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> PublishAsync(
            Guid eventId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.PublishAsync(GetActor(httpContext), eventId, GetCorrelationId(httpContext), cancellationToken));
        }

        async Task<IResult> CloseRegistrationAsync(
            Guid eventId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.CloseRegistrationAsync(GetActor(httpContext), eventId, GetCorrelationId(httpContext), cancellationToken));
        }

        async Task<IResult> StartAsync(
            Guid eventId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.StartAsync(GetActor(httpContext), eventId, GetCorrelationId(httpContext), cancellationToken));
        }

        async Task<IResult> CompleteAsync(
            Guid eventId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.CompleteAsync(GetActor(httpContext), eventId, GetCorrelationId(httpContext), cancellationToken));
        }

        async Task<IResult> CancelAsync(
            Guid eventId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.CancelAsync(GetActor(httpContext), eventId, GetCorrelationId(httpContext), cancellationToken));
        }

        async Task<IResult> AddPositionAsync(
            Guid eventId,
            CreateEventPositionRequest request,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var position = await service.AddPositionAsync(
                    GetActor(httpContext),
                    eventId,
                    new CreateEventPositionCommand(
                        request.Name,
                        request.Description,
                        request.Capacity,
                        request.RequiredDepartmentId,
                        request.SortOrder),
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Created($"/api/v1/events/{eventId}/positions/{position.Id}", position);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                or EventAccessDeniedException
                or EventNotFoundException
                or EventValidationException
                or EventConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> UpdatePositionAsync(
            Guid eventId,
            Guid positionId,
            UpdateEventPositionRequest request,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.UpdatePositionAsync(
                    GetActor(httpContext),
                    eventId,
                    positionId,
                    new UpdateEventPositionCommand(
                        request.Name,
                        request.Description,
                        request.Capacity,
                        request.RequiredDepartmentId,
                        request.SortOrder),
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> RegisterAsync(
            Guid eventId,
            Guid positionId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var registration = await service.RegisterAsync(
                    GetActor(httpContext),
                    eventId,
                    positionId,
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Created(
                    $"/api/v1/events/{eventId}/registrations/{registration.Id}",
                    registration);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                or EventAccessDeniedException
                or EventNotFoundException
                or EventPositionNotFoundException
                or EventValidationException
                or EventConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> ListRegistrationsAsync(
            Guid eventId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                httpContext,
                () => service.ListRegistrationsAsync(GetActor(httpContext), eventId, cancellationToken));
        }

        async Task<IResult> ApproveRegistrationAsync(
            Guid eventId,
            Guid registrationId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.ApproveRegistrationAsync(
                    GetActor(httpContext),
                    eventId,
                    registrationId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> RejectRegistrationAsync(
            Guid eventId,
            Guid registrationId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.RejectRegistrationAsync(
                    GetActor(httpContext),
                    eventId,
                    registrationId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> AssignAsync(
            Guid eventId,
            Guid positionId,
            AssignEventPositionRequest request,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var registration = await service.AssignAsync(
                    GetActor(httpContext),
                    eventId,
                    positionId,
                    request.MemberId,
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Created(
                    $"/api/v1/events/{eventId}/registrations/{registration.Id}",
                    registration);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                or EventAccessDeniedException
                or EventNotFoundException
                or EventPositionNotFoundException
                or EventValidationException
                or EventConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> CheckInAsync(
            Guid eventId,
            CheckInEventRequest request,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(
                httpContext,
                () => service.CheckInAsync(
                    GetActor(httpContext),
                    eventId,
                    request.MemberId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> ListAttendanceAsync(
            Guid eventId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                httpContext,
                () => service.ListAttendanceAsync(GetActor(httpContext), eventId, cancellationToken));
        }

        async Task<IResult> ListMemberHistoryAsync(
            Guid memberId,
            HttpContext httpContext,
            IEventManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                httpContext,
                () => service.ListMemberHistoryAsync(GetActor(httpContext), memberId, cancellationToken));
        }
    }

    private static async Task<IResult> ExecuteAsync<T>(HttpContext httpContext, Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or EventAccessDeniedException
            or EventNotFoundException
            or EventPositionNotFoundException
            or EventRegistrationNotFoundException
            or EventMemberNotFoundException)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> ExecuteMutationAsync<T>(HttpContext httpContext, Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or EventAccessDeniedException
            or EventNotFoundException
            or EventPositionNotFoundException
            or EventRegistrationNotFoundException
            or EventMemberNotFoundException
            or EventValidationException
            or EventConflictException)
        {
            return ToProblem(exception);
        }
    }

    private static Guid GetActor(HttpContext httpContext)
    {
        return WebIdentityPrincipal.TryGetMemberId(httpContext.User, out var actorMemberId)
            ? actorMemberId
            : throw new UnauthorizedAccessException();
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        return httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
    }

    private static IResult ToProblem(Exception exception)
    {
        var statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            EventAccessDeniedException => StatusCodes.Status403Forbidden,
            EventNotFoundException or EventPositionNotFoundException or EventRegistrationNotFoundException or EventMemberNotFoundException => StatusCodes.Status404NotFound,
            EventConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(statusCode: statusCode, detail: exception.Message);
    }

    public sealed record CreateEventRequest(
        string Name,
        string? Description,
        string? Location,
        DateTimeOffset StartsAt,
        DateTimeOffset? EndsAt,
        bool AllowMultiplePositions = false);

    public sealed record UpdateEventRequest(
        string? Name = null,
        string? Description = null,
        string? Location = null,
        DateTimeOffset? StartsAt = null,
        DateTimeOffset? EndsAt = null,
        bool? AllowMultiplePositions = null);

    public sealed record CreateEventPositionRequest(
        string Name,
        string? Description,
        int Capacity,
        Guid? RequiredDepartmentId,
        int SortOrder);

    public sealed record UpdateEventPositionRequest(
        string? Name = null,
        string? Description = null,
        int? Capacity = null,
        Guid? RequiredDepartmentId = null,
        int? SortOrder = null);

    public sealed record AssignEventPositionRequest(Guid MemberId);

    public sealed record CheckInEventRequest(Guid MemberId);
}
