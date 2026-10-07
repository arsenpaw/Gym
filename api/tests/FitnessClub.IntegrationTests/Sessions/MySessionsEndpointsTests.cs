using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;
using FitnessClub.Application.Training;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Sessions;

public class MySessionsEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private const string BaseUrl = "/api/sessions";
    private const string TestUserSubject = "test-user";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SessionSeeder _seed = new(factory);

    private async Task<SessionResponse> ScheduleAsync(Guid trainerId, DateTimeOffset start)
    {
        var room = await _seed.RoomAsync();
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, new
        {
            title = "Yoga", type = "Group", trainerId, roomId = room.Id, start, end = start.AddHours(1), capacity = 5,
        }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(Ct))!;
    }

    [Fact]
    public async Task Mine_returns_only_the_calling_trainers_sessions_in_range()
    {
        var me = await _seed.TrainerAsync(TestUserSubject);
        var other = await _seed.TrainerAsync();
        var later = await ScheduleAsync(me.Id, _seed.SlotStart(daysAhead: 2, hour: 15));
        var earlier = await ScheduleAsync(me.Id, _seed.SlotStart(daysAhead: 2, hour: 9));
        var outOfRange = await ScheduleAsync(me.Id, _seed.SlotStart(daysAhead: 4, hour: 9));
        await ScheduleAsync(other.Id, _seed.SlotStart(daysAhead: 2, hour: 9));
        var from = _seed.SlotStart(daysAhead: 2, hour: 0);
        var to = from.AddDays(1);

        var response = await factory.CreateClientWithRoles(Roles.Trainer).GetAsync(
            $"{BaseUrl}/mine?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessions = await response.Content.ReadFromJsonAsync<List<SessionSummaryResponse>>(Ct);
        Assert.Equal([earlier.Id, later.Id], sessions!.Select(s => s.Id));
        Assert.All(sessions!, s => Assert.Equal(me.Id, s.TrainerId));
        Assert.DoesNotContain(sessions!, s => s.Id == outOfRange.Id);
    }
}
