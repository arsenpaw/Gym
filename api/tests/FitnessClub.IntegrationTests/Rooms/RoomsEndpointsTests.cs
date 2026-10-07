using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;
using FitnessClub.Application.Rooms;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Rooms;

public class RoomsEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private const string BaseUrl = "/api/rooms";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object NewRoom(int capacity = 20) =>
        new { name = $"Room {Guid.NewGuid():N}", capacity };

    private async Task<RoomResponse> CreateRoomAsync(object? body = null)
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, body ?? NewRoom(), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(Ct))!;
    }

    [Fact]
    public async Task Create_as_admin_returns_201_with_location()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, NewRoom(capacity: 30), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>(Ct);
        Assert.NotNull(room);
        Assert.Equal(30, room.Capacity);
        Assert.True(room.IsActive);
        Assert.Equal($"{BaseUrl}/{room.Id}", response.Headers.Location?.AbsolutePath, ignoreCase: true);
    }

    [Fact]
    public async Task Create_without_user_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(BaseUrl, NewRoom(), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Receptionist)]
    [InlineData(Roles.Trainer)]
    public async Task Create_as_non_admin_returns_403(string role)
    {
        var response = await factory.CreateClientWithRoles(role).PostAsJsonAsync(BaseUrl, NewRoom(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "name": "", "capacity": 10 }""")]
    [InlineData("""{ "name": "No capacity" }""")]
    [InlineData("""{ "name": "Zero capacity", "capacity": 0 }""")]
    [InlineData("""{ "name": "Huge capacity", "capacity": 501 }""")]
    [InlineData("""{ "name": "   ", "capacity": 10 }""")]
    [InlineData("""{ "name": "Text capacity", "capacity": "abc" }""")]
    [InlineData("""{ "name": "Not json" """)]
    public async Task Create_with_invalid_body_returns_400_problem_details(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsync(BaseUrl, content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_with_name_longer_than_100_chars_returns_400()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin)
            .PostAsJsonAsync(BaseUrl, new { name = new string('a', 101), capacity = 10 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_duplicate_name_returns_409()
    {
        var existing = await CreateRoomAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin)
            .PostAsJsonAsync(BaseUrl, new { name = $"  {existing.Name.ToUpperInvariant()} ", capacity = 10 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Receptionist)]
    [InlineData(Roles.Trainer)]
    public async Task Get_as_staff_returns_room(string role)
    {
        var created = await CreateRoomAsync();

        var room = await factory.CreateClientWithRoles(role).GetFromJsonAsync<RoomResponse>($"{BaseUrl}/{created.Id}", Ct);

        Assert.Equal(created, room);
    }

    [Fact]
    public async Task List_as_trainer_returns_rooms()
    {
        var created = await CreateRoomAsync();

        var rooms = await factory.CreateClientWithRoles(Roles.Trainer).GetFromJsonAsync<List<RoomResponse>>(BaseUrl, Ct);

        Assert.Contains(created, rooms!);
    }

    [Fact]
    public async Task List_without_user_returns_401()
    {
        var response = await factory.CreateClient().GetAsync(BaseUrl, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_with_malformed_id_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{BaseUrl}/not-a-guid", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_missing_room_returns_404_problem_details()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{BaseUrl}/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Update_as_admin_returns_updated_room()
    {
        var created = await CreateRoomAsync();
        var newName = $"Renamed {Guid.NewGuid():N}";

        var response = await factory.CreateClientWithRoles(Roles.Admin)
            .PutAsJsonAsync($"{BaseUrl}/{created.Id}", new { name = newName, capacity = 45 }, Ct);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>(Ct);
        Assert.Equal(created with { Name = newName, Capacity = 45 }, updated);
    }

    [Fact]
    public async Task Update_keeping_own_name_returns_200()
    {
        var created = await CreateRoomAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin)
            .PutAsJsonAsync($"{BaseUrl}/{created.Id}", new { name = created.Name, capacity = 5 }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_to_another_rooms_name_returns_409()
    {
        var first = await CreateRoomAsync();
        var second = await CreateRoomAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin)
            .PutAsJsonAsync($"{BaseUrl}/{second.Id}", new { name = first.Name, capacity = 10 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_with_invalid_capacity_returns_400()
    {
        var created = await CreateRoomAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin)
            .PutAsJsonAsync($"{BaseUrl}/{created.Id}", new { name = created.Name, capacity = 0 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_missing_room_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", NewRoom(), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Receptionist)]
    [InlineData(Roles.Trainer)]
    public async Task Update_as_non_admin_returns_403(string role)
    {
        var created = await CreateRoomAsync();

        var response = await factory.CreateClientWithRoles(role).PutAsJsonAsync($"{BaseUrl}/{created.Id}", NewRoom(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_room_is_hidden_from_default_list_and_back_after_activate()
    {
        var room = await CreateRoomAsync();
        var admin = factory.CreateClientWithRoles(Roles.Admin);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"{BaseUrl}/{room.Id}/deactivate", null, Ct)).StatusCode);
        var activeOnly = await admin.GetFromJsonAsync<List<RoomResponse>>(BaseUrl, Ct);
        var all = await admin.GetFromJsonAsync<List<RoomResponse>>($"{BaseUrl}?includeInactive=true", Ct);
        Assert.DoesNotContain(activeOnly!, r => r.Id == room.Id);
        Assert.Contains(all!, r => r.Id == room.Id && !r.IsActive);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"{BaseUrl}/{room.Id}/activate", null, Ct)).StatusCode);
        var afterActivate = await admin.GetFromJsonAsync<List<RoomResponse>>(BaseUrl, Ct);
        Assert.Contains(afterActivate!, r => r.Id == room.Id && r.IsActive);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task Activation_of_missing_room_returns_404(string action)
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsync($"{BaseUrl}/{Guid.NewGuid()}/{action}", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Receptionist, "activate")]
    [InlineData(Roles.Receptionist, "deactivate")]
    [InlineData(Roles.Trainer, "activate")]
    [InlineData(Roles.Trainer, "deactivate")]
    public async Task Activation_as_non_admin_returns_403(string role, string action)
    {
        var room = await CreateRoomAsync();

        var response = await factory.CreateClientWithRoles(role).PostAsync($"{BaseUrl}/{room.Id}/{action}", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
