using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;
using FitnessClub.Application.Training;
using FitnessClub.Domain.Training;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.IntegrationTests.Sessions;

public class SessionsEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private const string BaseUrl = "/api/sessions";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SessionSeeder _seed = new(factory);

    private HttpClient Admin => factory.CreateClientWithRoles(Roles.Admin);

    private static string Range(DateTimeOffset from, DateTimeOffset to) =>
        $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

    private async Task<object> NewSessionAsync(DateTimeOffset? start = null, int durationMinutes = 60, int capacity = 10, string type = "Group")
    {
        var trainer = await _seed.TrainerAsync();
        var room = await _seed.RoomAsync();
        var from = start ?? _seed.SlotStart();
        return new { title = "Morning yoga", type, trainerId = trainer.Id, roomId = room.Id, start = from, end = from.AddMinutes(durationMinutes), capacity };
    }

    private async Task<SessionResponse> ScheduleAsync(DateTimeOffset? start = null, int capacity = 10)
    {
        var response = await Admin.PostAsJsonAsync(BaseUrl, await NewSessionAsync(start, capacity: capacity), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(Ct))!;
    }

    private Task<HttpResponseMessage> BookAsync(Guid sessionId, Guid clientId, HttpClient? http = null) =>
        (http ?? Admin).PostAsJsonAsync($"{BaseUrl}/{sessionId}/bookings", new { clientId }, Ct);

    private Task<SessionResponse?> GetAsync(Guid id) => Admin.GetFromJsonAsync<SessionResponse>($"{BaseUrl}/{id}", Ct);

    [Fact]
    public async Task Schedule_as_receptionist_returns_201_with_location()
    {
        var trainer = await _seed.TrainerAsync();
        var room = await _seed.RoomAsync();
        var start = _seed.SlotStart();

        var response = await factory.CreateClientWithRoles(Roles.Receptionist).PostAsJsonAsync(BaseUrl, new
        {
            title = "Personal training", type = "Individual", trainerId = trainer.Id, roomId = room.Id,
            start, end = start.AddMinutes(45), capacity = 1,
        }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(Ct);
        Assert.NotNull(session);
        Assert.Equal(SessionType.Individual, session.Type);
        Assert.Equal(SessionStatus.Scheduled, session.Status);
        Assert.Equal(start, session.Start);
        Assert.Equal(start.AddMinutes(45), session.End);
        Assert.Equal(1, session.Capacity);
        Assert.Empty(session.Bookings);
        Assert.Equal($"{BaseUrl}/{session.Id}", response.Headers.Location?.AbsolutePath, ignoreCase: true);
    }

    [Fact]
    public async Task Schedule_returns_enums_as_strings()
    {
        var response = await Admin.PostAsJsonAsync(BaseUrl, await NewSessionAsync(), Ct);

        var json = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("\"type\":\"Group\"", json);
        Assert.Contains("\"status\":\"Scheduled\"", json);
    }

    [Fact]
    public async Task Schedule_without_user_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(BaseUrl, await NewSessionAsync(), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Schedule_as_trainer_returns_403()
    {
        var response = await factory.CreateClientWithRoles(Roles.Trainer).PostAsJsonAsync(BaseUrl, await NewSessionAsync(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "title": "", "type": "Group", "trainerId": "TRAINER", "roomId": "ROOM", "start": "START", "end": "END", "capacity": 5 }""")]
    [InlineData("""{ "title": "Yoga", "trainerId": "TRAINER", "roomId": "ROOM", "start": "START", "end": "END", "capacity": 5 }""")]
    [InlineData("""{ "title": "Yoga", "type": "Zumba", "trainerId": "TRAINER", "roomId": "ROOM", "start": "START", "end": "END", "capacity": 5 }""")]
    [InlineData("""{ "title": "Yoga", "type": "Group", "roomId": "ROOM", "start": "START", "end": "END", "capacity": 5 }""")]
    [InlineData("""{ "title": "Yoga", "type": "Group", "trainerId": "TRAINER", "start": "START", "end": "END", "capacity": 5 }""")]
    [InlineData("""{ "title": "Yoga", "type": "Group", "trainerId": "TRAINER", "roomId": "ROOM", "end": "END", "capacity": 5 }""")]
    [InlineData("""{ "title": "Yoga", "type": "Group", "trainerId": "TRAINER", "roomId": "ROOM", "start": "START", "end": "END", "capacity": 0 }""")]
    [InlineData("""{ "title": "Yoga", "type": "Group", "trainerId": "TRAINER", "roomId": "ROOM", "start": "START", "end": "END" }""")]
    [InlineData("""{ "title": "Yoga", "type": 7, "trainerId": "TRAINER", "roomId": "ROOM", "start": "START", "end": "END", "capacity": 5 }""")]
    [InlineData("""{ "title": "Not json" """)]
    public async Task Schedule_with_invalid_body_returns_400_problem_details(string template)
    {
        var trainer = await _seed.TrainerAsync();
        var room = await _seed.RoomAsync();
        var start = _seed.SlotStart(daysAhead: 9);
        var json = template
            .Replace("TRAINER", trainer.Id.ToString())
            .Replace("ROOM", room.Id.ToString())
            .Replace("START", start.ToString("O"))
            .Replace("END", start.AddHours(1).ToString("O"));
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await Admin.PostAsync(BaseUrl, content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Schedule_with_title_longer_than_limit_returns_400()
    {
        var trainer = await _seed.TrainerAsync();
        var room = await _seed.RoomAsync();
        var start = _seed.SlotStart();

        var response = await Admin.PostAsJsonAsync(BaseUrl, new
        {
            title = new string('a', TrainingSession.TitleMaxLength + 1), type = "Group", trainerId = trainer.Id, roomId = room.Id,
            start, end = start.AddHours(1), capacity = 5,
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Schedule_with_end_before_start_returns_400_from_domain_rule()
    {
        var response = await Admin.PostAsJsonAsync(BaseUrl, await NewSessionAsync(durationMinutes: -60), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("A time slot must end after it starts.", problem?.Detail);
    }

    [Fact]
    public async Task Schedule_in_the_past_returns_400()
    {
        var response = await Admin.PostAsJsonAsync(BaseUrl, await NewSessionAsync(_seed.SlotStart(daysAhead: -1)), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("A session must be scheduled in the future.", problem?.Detail);
    }

    [Fact]
    public async Task Schedule_in_a_room_that_is_taken_returns_400()
    {
        var room = await _seed.RoomAsync();
        var start = _seed.SlotStart();
        object Body(Guid trainerId) => new { title = "Yoga", type = "Group", trainerId, roomId = room.Id, start, end = start.AddHours(1), capacity = 5 };
        (await Admin.PostAsJsonAsync(BaseUrl, Body((await _seed.TrainerAsync()).Id), Ct)).EnsureSuccessStatusCode();

        var response = await Admin.PostAsJsonAsync(BaseUrl, Body((await _seed.TrainerAsync()).Id), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Schedule_with_unknown_trainer_returns_404()
    {
        var room = await _seed.RoomAsync();
        var start = _seed.SlotStart();

        var response = await Admin.PostAsJsonAsync(BaseUrl, new
        {
            title = "Yoga", type = "Group", trainerId = Guid.NewGuid(), roomId = room.Id, start, end = start.AddHours(1), capacity = 5,
        }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Schedule_with_unknown_room_returns_404()
    {
        var trainer = await _seed.TrainerAsync();
        var start = _seed.SlotStart();

        var response = await Admin.PostAsJsonAsync(BaseUrl, new
        {
            title = "Yoga", type = "Group", trainerId = trainer.Id, roomId = Guid.NewGuid(), start, end = start.AddHours(1), capacity = 5,
        }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_as_trainer_returns_session()
    {
        var created = await ScheduleAsync();

        var session = await factory.CreateClientWithRoles(Roles.Trainer).GetFromJsonAsync<SessionResponse>($"{BaseUrl}/{created.Id}", Ct);

        Assert.Equal(created.Id, session?.Id);
        Assert.Equal(created.Title, session?.Title);
    }

    [Fact]
    public async Task Get_missing_session_returns_404()
    {
        var response = await Admin.GetAsync($"{BaseUrl}/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_without_user_returns_401()
    {
        var response = await factory.CreateClient().GetAsync($"{BaseUrl}/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_sessions_starting_in_range_with_booking_counts()
    {
        var late = await ScheduleAsync(_seed.SlotStart(daysAhead: 5, hour: 15));
        var early = await ScheduleAsync(_seed.SlotStart(daysAhead: 5, hour: 9));
        var nextDay = await ScheduleAsync(_seed.SlotStart(daysAhead: 6, hour: 9));
        var client = await _seed.ClientAsync();
        (await BookAsync(early.Id, client.Id)).EnsureSuccessStatusCode();
        var from = _seed.SlotStart(daysAhead: 5, hour: 0);

        var list = await factory.CreateClientWithRoles(Roles.Trainer)
            .GetFromJsonAsync<List<SessionSummaryResponse>>($"{BaseUrl}?{Range(from, from.AddDays(1))}", Ct);

        var ids = list!.Select(s => s.Id).ToList();
        Assert.True(ids.IndexOf(early.Id) >= 0 && ids.IndexOf(early.Id) < ids.IndexOf(late.Id));
        Assert.DoesNotContain(nextDay.Id, ids);
        Assert.Equal(1, list!.Single(s => s.Id == early.Id).ActiveBookingCount);
        Assert.Equal(0, list!.Single(s => s.Id == late.Id).ActiveBookingCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-10-01T00:00:00Z")]
    [InlineData("?from=2026-10-08T00:00:00Z&to=2026-10-01T00:00:00Z")]
    [InlineData("?from=2026-01-01T00:00:00Z&to=2026-12-31T00:00:00Z")]
    [InlineData("?from=yesterday&to=tomorrow")]
    public async Task List_with_invalid_range_returns_400(string query)
    {
        var response = await Admin.GetAsync($"{BaseUrl}{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Book_returns_201_and_session_shows_booking()
    {
        var session = await ScheduleAsync();
        var client = await _seed.ClientAsync();

        var response = await BookAsync(session.Id, client.Id, factory.CreateClientWithRoles(Roles.Receptionist));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{BaseUrl}/{session.Id}", response.Headers.Location?.AbsolutePath, ignoreCase: true);
        var booking = await response.Content.ReadFromJsonAsync<BookingResponse>(Ct);
        Assert.Equal(client.Id, booking?.ClientId);
        Assert.Null(booking?.CancelledAt);
        var loaded = await GetAsync(session.Id);
        Assert.Equal(1, loaded!.ActiveBookingCount);
        Assert.Equal(client.Id, Assert.Single(loaded.Bookings).ClientId);
    }

    [Fact]
    public async Task Book_as_trainer_returns_403()
    {
        var session = await ScheduleAsync();
        var client = await _seed.ClientAsync();

        var response = await BookAsync(session.Id, client.Id, factory.CreateClientWithRoles(Roles.Trainer));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Book_without_client_id_returns_400()
    {
        var session = await ScheduleAsync();

        var response = await Admin.PostAsJsonAsync($"{BaseUrl}/{session.Id}/bookings", new { }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Book_unknown_client_returns_404()
    {
        var session = await ScheduleAsync();

        var response = await BookAsync(session.Id, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Book_unknown_session_returns_404()
    {
        var client = await _seed.ClientAsync();

        var response = await BookAsync(Guid.NewGuid(), client.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Book_client_without_membership_returns_400()
    {
        var session = await ScheduleAsync();
        var client = await _seed.ClientAsync(withMembership: false);

        var response = await BookAsync(session.Id, client.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("The client has no active membership on the session date.", problem?.Detail);
    }

    [Fact]
    public async Task Book_same_client_twice_returns_400()
    {
        var session = await ScheduleAsync();
        var client = await _seed.ClientAsync();
        (await BookAsync(session.Id, client.Id)).EnsureSuccessStatusCode();

        var response = await BookAsync(session.Id, client.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Book_full_session_returns_400()
    {
        var session = await ScheduleAsync(capacity: 1);
        (await BookAsync(session.Id, (await _seed.ClientAsync()).Id)).EnsureSuccessStatusCode();

        var response = await BookAsync(session.Id, (await _seed.ClientAsync()).Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_bookings_for_the_last_place_never_overbook()
    {
        var session = await ScheduleAsync(capacity: 1);
        var first = await _seed.ClientAsync();
        var second = await _seed.ClientAsync();

        var responses = await Task.WhenAll(BookAsync(session.Id, first.Id), BookAsync(session.Id, second.Id));

        var codes = responses.Select(r => r.StatusCode).OrderBy(c => c).ToList();
        Assert.Equal(HttpStatusCode.Created, codes[0]);
        Assert.Contains(codes[1], new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict });
        Assert.Single((await GetAsync(session.Id))!.Bookings);
    }

    [Fact]
    public async Task CancelBooking_returns_204_and_frees_the_place()
    {
        var session = await ScheduleAsync(capacity: 1);
        var client = await _seed.ClientAsync();
        (await BookAsync(session.Id, client.Id)).EnsureSuccessStatusCode();

        var response = await factory.CreateClientWithRoles(Roles.Receptionist)
            .PostAsync($"{BaseUrl}/{session.Id}/bookings/{client.Id}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var loaded = await GetAsync(session.Id);
        Assert.Equal(0, loaded!.ActiveBookingCount);
        Assert.NotNull(Assert.Single(loaded.Bookings).CancelledAt);
        Assert.Equal(HttpStatusCode.Created, (await BookAsync(session.Id, (await _seed.ClientAsync()).Id)).StatusCode);
    }

    [Fact]
    public async Task CancelBooking_for_client_without_booking_returns_400()
    {
        var session = await ScheduleAsync();

        var response = await Admin.PostAsync($"{BaseUrl}/{session.Id}/bookings/{Guid.NewGuid()}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_for_missing_session_returns_404()
    {
        var response = await Admin.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/bookings/{Guid.NewGuid()}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_as_trainer_returns_403()
    {
        var response = await factory.CreateClientWithRoles(Roles.Trainer)
            .PostAsync($"{BaseUrl}/{Guid.NewGuid()}/bookings/{Guid.NewGuid()}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_returns_204_and_cancels_bookings()
    {
        var session = await ScheduleAsync();
        (await BookAsync(session.Id, (await _seed.ClientAsync()).Id)).EnsureSuccessStatusCode();

        var response = await factory.CreateClientWithRoles(Roles.Receptionist).PostAsync($"{BaseUrl}/{session.Id}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var loaded = await GetAsync(session.Id);
        Assert.Equal(SessionStatus.Cancelled, loaded!.Status);
        Assert.Equal(0, loaded.ActiveBookingCount);
        Assert.NotNull(Assert.Single(loaded.Bookings).CancelledAt);
    }

    [Fact]
    public async Task Cancel_twice_returns_400_and_booking_a_cancelled_session_returns_400()
    {
        var session = await ScheduleAsync();
        (await Admin.PostAsync($"{BaseUrl}/{session.Id}/cancel", null, Ct)).EnsureSuccessStatusCode();

        var second = await Admin.PostAsync($"{BaseUrl}/{session.Id}/cancel", null, Ct);
        var booking = await BookAsync(session.Id, (await _seed.ClientAsync()).Id);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, booking.StatusCode);
    }

    [Fact]
    public async Task Cancelled_session_frees_the_room_for_a_new_session()
    {
        var room = await _seed.RoomAsync();
        var start = _seed.SlotStart(daysAhead: 7);
        object Body(Guid trainerId) => new { title = "Yoga", type = "Group", trainerId, roomId = room.Id, start, end = start.AddHours(1), capacity = 5 };
        var first = await Admin.PostAsJsonAsync(BaseUrl, Body((await _seed.TrainerAsync()).Id), Ct);
        var created = await first.Content.ReadFromJsonAsync<SessionResponse>(Ct);
        (await Admin.PostAsync($"{BaseUrl}/{created!.Id}/cancel", null, Ct)).EnsureSuccessStatusCode();

        var response = await Admin.PostAsJsonAsync(BaseUrl, Body((await _seed.TrainerAsync()).Id), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_missing_session_returns_404()
    {
        var response = await Admin.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_as_trainer_returns_403()
    {
        var session = await ScheduleAsync();

        var response = await factory.CreateClientWithRoles(Roles.Trainer).PostAsync($"{BaseUrl}/{session.Id}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Receptionist)]
    public async Task Mine_for_non_trainer_returns_403(string role)
    {
        var from = _seed.SlotStart(daysAhead: 0, hour: 0);

        var response = await factory.CreateClientWithRoles(role).GetAsync($"{BaseUrl}/mine?{Range(from, from.AddDays(7))}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mine_without_linked_trainer_returns_404()
    {
        var from = _seed.SlotStart(daysAhead: 0, hour: 0);

        var response = await factory.CreateClientWithRoles(Roles.Trainer).GetAsync($"{BaseUrl}/mine?{Range(from, from.AddDays(7))}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
