using Felion.Domain.Events;
using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.IntegrationTests;

public sealed class PostgreSqlEventAttendanceTests
{
    [PostgreSqlFact]
    public async Task ConcurrentAttendanceInsertsLeaveExactlyOneRecord()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (eventId, memberId, coreMemberId) = await SeedAttendancePrerequisitesAsync(database);

        var results = await Task.WhenAll(
            InsertAttendanceAsync(database, eventId, memberId, coreMemberId),
            InsertAttendanceAsync(database, eventId, memberId, coreMemberId));

        Assert.Equal(1, results.Count(result => result is null));
        var conflict = Assert.Single(results, result => result is not null);
        Assert.Equal("23505", Assert.IsType<PostgresException>(conflict!.InnerException).SqlState);

        await using var verificationContext = database.CreateContext();
        Assert.Equal(
            1,
            await verificationContext.EventAttendances.CountAsync(
                attendance => attendance.EventId == eventId && attendance.MemberId == memberId));
    }

    private static async Task<(Guid EventId, Guid MemberId, Guid CoreMemberId)> SeedAttendancePrerequisitesAsync(
        PostgreSqlTestDatabase database)
    {
        await using var context = database.CreateContext();
        var department = Department.CreateRegular("Event Test", "event-test");
        var generation = Generation.Create("Event Test", "EVENT-TEST");
        var core = Member.Create(
            "CORE-EVENT-TEST",
            "Core Event Test",
            "core-event-test@example.org",
            Department.CoreDepartmentId,
            true,
            generation.Id,
            MemberPosition.Core);
        var member = Member.Create(
            "MEMBER-EVENT-TEST",
            "Member Event Test",
            "member-event-test@example.org",
            department.Id,
            false,
            generation.Id,
            MemberPosition.Member);
        var @event = Event.Create(
            "Event Attendance Test",
            null,
            null,
            DateTimeOffset.UtcNow,
            null,
            false,
            core.Id);

        context.Departments.Add(department);
        context.Generations.Add(generation);
        context.Members.AddRange(core, member);
        context.Events.Add(@event);
        await context.SaveChangesAsync();

        return (@event.Id, member.Id, core.Id);
    }

    private static async Task<DbUpdateException?> InsertAttendanceAsync(
        PostgreSqlTestDatabase database,
        Guid eventId,
        Guid memberId,
        Guid coreMemberId)
    {
        await using var context = database.CreateContext();
        context.EventAttendances.Add(EventAttendance.CheckIn(eventId, memberId, coreMemberId));

        try
        {
            await context.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateException exception)
        {
            return exception;
        }
    }
}
