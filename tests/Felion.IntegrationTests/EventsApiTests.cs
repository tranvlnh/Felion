using System.Net;
using System.Net.Http.Json;
using Felion.Application.Events;
using Felion.Application.Identity;
using Felion.Domain.Events;
using Felion.Domain.Members;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Felion.IntegrationTests;

public sealed class EventsApiTests
{
    [Fact]
    public async Task CheckInRequiresAnAuthenticatedCoreOrAdmin()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = Guid.NewGuid();

        using var anonymousResponse = await client.PostAsJsonAsync(
            $"/api/v1/events/{eventId}/attendance",
            new { MemberId = factory.Member.MemberId });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var memberRequest = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/events/{eventId}/attendance",
            factory.Member.MemberId,
            new { MemberId = factory.Member.MemberId });
        using var memberResponse = await client.SendAsync(memberRequest);

        Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);
        Assert.Empty(factory.Events.CheckIns);
    }

    [Fact]
    public async Task CoreCheckInFlowsThroughTheHttpEndpointWithCorrelationId()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString("N");
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/events/{eventId}/attendance",
            factory.Core.MemberId,
            new { MemberId = factory.Member.MemberId });
        request.Headers.Add("X-Correlation-Id", correlationId);

        using var response = await client.SendAsync(request);
        var attendance = await response.Content.ReadFromJsonAsync<EventAttendanceDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(attendance);
        Assert.Equal(eventId, attendance.EventId);
        var checkIn = Assert.Single(factory.Events.CheckIns);
        Assert.Equal(factory.Core.MemberId, checkIn.ActorMemberId);
        Assert.Equal(factory.Member.MemberId, checkIn.MemberId);
        Assert.Equal(correlationId, checkIn.CorrelationId);
    }

    [Fact]
    public async Task CheckInConflictIsReturnedAsProblemDetails()
    {
        using var factory = new TestApplicationFactory();
        factory.Events.CheckInFailure = new EventConflictException("Attendance already exists.");
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/events/{Guid.NewGuid()}/attendance",
            factory.Core.MemberId,
            new { MemberId = factory.Member.MemberId });

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Attendance already exists.", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttendanceReportingRequiresCoreOrAdmin()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = Guid.NewGuid();

        using var memberRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/events/{eventId}/attendance",
            factory.Member.MemberId);
        using var memberResponse = await client.SendAsync(memberRequest);
        Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);

        using var coreRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/events/{eventId}/attendance",
            factory.Core.MemberId);
        using var coreResponse = await client.SendAsync(coreRequest);

        Assert.Equal(HttpStatusCode.OK, coreResponse.StatusCode);
        Assert.Equal(factory.Core.MemberId, Assert.Single(factory.Events.AttendanceListActors));
    }

    [Fact]
    public async Task ActiveMemberCanRequestOwnEventHistory()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/members/{factory.Member.MemberId}/event-history",
            factory.Member.MemberId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requestDetails = Assert.Single(factory.Events.MemberHistoryRequests);
        Assert.Equal(factory.Member.MemberId, requestDetails.ActorMemberId);
        Assert.Equal(factory.Member.MemberId, requestDetails.MemberId);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string uri,
        Guid actorMemberId,
        object? content = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(WebIdentityHeaders.TemporaryActorMemberId, actorMemberId.ToString("D"));
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return request;
    }

    public sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        public WebMemberIdentity Core { get; } = new(
            Guid.NewGuid(),
            "Core User",
            "core@example.org",
            MemberPosition.Core);

        public WebMemberIdentity Member { get; } = new(
            Guid.NewGuid(),
            "Member User",
            "member@example.org",
            MemberPosition.Member);

        public TestEventManagementService Events { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = string.Empty
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWebIdentityService>();
                services.AddSingleton<IWebIdentityService>(new TestWebIdentityService([Core, Member]));
                services.RemoveAll<IEventManagementService>();
                services.AddSingleton<IEventManagementService>(Events);
            });
        }
    }

    public sealed class TestWebIdentityService(IReadOnlyCollection<WebMemberIdentity> members) : IWebIdentityService
    {
        public Task<WebMemberIdentity?> ResolveActiveMemberByEmailAsync(string? email, CancellationToken cancellationToken)
        {
            return Task.FromResult(members.SingleOrDefault(member => member.ClubEmail == email));
        }

        public Task<WebMemberIdentity?> ResolveActiveMemberByIdAsync(Guid memberId, CancellationToken cancellationToken)
        {
            return Task.FromResult(members.SingleOrDefault(member => member.MemberId == memberId));
        }
    }

    public sealed class TestEventManagementService : IEventManagementService
    {
        public Exception? CheckInFailure { get; set; }

        public List<CheckInRequest> CheckIns { get; } = [];

        public List<Guid> AttendanceListActors { get; } = [];

        public List<MemberHistoryRequest> MemberHistoryRequests { get; } = [];

        public Task<EventAttendanceDto> CheckInAsync(
            Guid actorMemberId,
            Guid eventId,
            Guid memberId,
            string correlationId,
            CancellationToken cancellationToken)
        {
            if (CheckInFailure is not null)
            {
                return Task.FromException<EventAttendanceDto>(CheckInFailure);
            }

            CheckIns.Add(new CheckInRequest(actorMemberId, eventId, memberId, correlationId));
            var timestamp = DateTimeOffset.UtcNow;
            return Task.FromResult(new EventAttendanceDto(
                Guid.NewGuid(),
                eventId,
                memberId,
                timestamp,
                actorMemberId,
                timestamp));
        }

        public Task<IReadOnlyList<EventAttendanceDto>> ListAttendanceAsync(
            Guid actorMemberId,
            Guid eventId,
            CancellationToken cancellationToken)
        {
            AttendanceListActors.Add(actorMemberId);
            return Task.FromResult<IReadOnlyList<EventAttendanceDto>>([]);
        }

        public Task<IReadOnlyList<MemberEventHistoryDto>> ListMemberHistoryAsync(
            Guid actorMemberId,
            Guid memberId,
            CancellationToken cancellationToken)
        {
            MemberHistoryRequests.Add(new MemberHistoryRequest(actorMemberId, memberId));
            return Task.FromResult<IReadOnlyList<MemberEventHistoryDto>>([]);
        }

        public Task<IReadOnlyList<EventDto>> ListAsync(Guid actorMemberId, CancellationToken cancellationToken) => Throw<IReadOnlyList<EventDto>>();

        public Task<EventDto> GetAsync(Guid actorMemberId, Guid eventId, CancellationToken cancellationToken) => Throw<EventDto>();

        public Task<EventDto> CreateAsync(Guid actorMemberId, CreateEventCommand command, string correlationId, CancellationToken cancellationToken) => Throw<EventDto>();

        public Task<EventDto> UpdateAsync(Guid actorMemberId, Guid eventId, UpdateEventCommand command, string correlationId, CancellationToken cancellationToken) => Throw<EventDto>();

        public Task<EventDto> PublishAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken) => Throw<EventDto>();

        public Task<EventDto> CloseRegistrationAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken) => Throw<EventDto>();

        public Task<EventDto> StartAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken) => Throw<EventDto>();

        public Task<EventDto> CompleteAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken) => Throw<EventDto>();

        public Task<EventDto> CancelAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken) => Throw<EventDto>();

        public Task<EventPositionDto> AddPositionAsync(Guid actorMemberId, Guid eventId, CreateEventPositionCommand command, string correlationId, CancellationToken cancellationToken) => Throw<EventPositionDto>();

        public Task<EventPositionDto> UpdatePositionAsync(Guid actorMemberId, Guid eventId, Guid positionId, UpdateEventPositionCommand command, string correlationId, CancellationToken cancellationToken) => Throw<EventPositionDto>();

        public Task<EventRegistrationDto> RegisterAsync(Guid actorMemberId, Guid eventId, Guid positionId, string correlationId, CancellationToken cancellationToken) => Throw<EventRegistrationDto>();

        public Task<IReadOnlyList<EventRegistrationDto>> ListRegistrationsAsync(Guid actorMemberId, Guid eventId, CancellationToken cancellationToken) => Throw<IReadOnlyList<EventRegistrationDto>>();

        public Task<EventRegistrationDto> ApproveRegistrationAsync(Guid actorMemberId, Guid eventId, Guid registrationId, string correlationId, CancellationToken cancellationToken) => Throw<EventRegistrationDto>();

        public Task<EventRegistrationDto> RejectRegistrationAsync(Guid actorMemberId, Guid eventId, Guid registrationId, string correlationId, CancellationToken cancellationToken) => Throw<EventRegistrationDto>();

        public Task<EventRegistrationDto> AssignAsync(Guid actorMemberId, Guid eventId, Guid positionId, Guid memberId, string correlationId, CancellationToken cancellationToken) => Throw<EventRegistrationDto>();

        private static Task<T> Throw<T>() => Task.FromException<T>(new NotSupportedException());
    }

    public sealed record CheckInRequest(Guid ActorMemberId, Guid EventId, Guid MemberId, string CorrelationId);

    public sealed record MemberHistoryRequest(Guid ActorMemberId, Guid MemberId);
}
