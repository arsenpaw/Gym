using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Application.Trainers;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Trainers;

public class TrainersEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private const string BaseUrl = "/api/trainers";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient Admin => factory.CreateClientWithRoles(Roles.Admin);

    private HttpClient Receptionist => factory.CreateClientWithRoles(Roles.Receptionist);

    private static string UniquePhone() => $"+380{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

    private static object NewTrainer(string? phone = null, string lastName = "Bondar", string? email = null) =>
        new { firstName = "Taras", lastName, middleName = (string?)null, phone = phone ?? UniquePhone(), email, specialization = "Yoga" };

    private async Task<TrainerResponse> HireAsync(object? body = null)
    {
        var response = await Admin.PostAsJsonAsync(BaseUrl, body ?? NewTrainer(), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TrainerResponse>(Ct))!;
    }

    private async Task<Guid> SeedClientAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetLocalNow();
        var client = Client.Register(
            PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), null, now);
        scope.ServiceProvider.GetRequiredService<IClientRepository>().Add(client);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return client.Id;
    }

    private Task<TrainerResponse?> GetAsync(Guid id) => Admin.GetFromJsonAsync<TrainerResponse>($"{BaseUrl}/{id}", Ct);

    [Fact]
    public async Task Hire_as_admin_returns_201_with_location()
    {
        var response = await Admin.PostAsJsonAsync(BaseUrl, NewTrainer(email: "Taras@Club.com"), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var trainer = await response.Content.ReadFromJsonAsync<TrainerResponse>(Ct);
        Assert.NotNull(trainer);
        Assert.True(trainer.IsActive);
        Assert.Equal("taras@club.com", trainer.Email);
        Assert.Equal("Bondar Taras", trainer.FullName);
        Assert.Empty(trainer.WorkingHours);
        Assert.Empty(trainer.Clients);
        Assert.Equal($"{BaseUrl}/{trainer.Id}", response.Headers.Location?.AbsolutePath, ignoreCase: true);
    }

    [Fact]
    public async Task Hire_without_user_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(BaseUrl, NewTrainer(), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hire_as_receptionist_returns_403()
    {
        var response = await Receptionist.PostAsJsonAsync(BaseUrl, NewTrainer(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_as_trainer_returns_403()
    {
        var response = await factory.CreateClientWithRoles(Roles.Trainer).GetAsync(BaseUrl, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "firstName": "", "lastName": "Bondar", "phone": "+380501112233", "specialization": "Yoga" }""")]
    [InlineData("""{ "firstName": "Taras", "lastName": "Bondar", "specialization": "Yoga" }""")]
    [InlineData("""{ "firstName": "Taras", "lastName": "Bondar", "phone": "+380501112233" }""")]
    [InlineData("""{ "firstName": "Taras", "lastName": "Bondar", "phone": "+380501112233", "specialization": "   " }""")]
    [InlineData("""{ "firstName": "Taras", "lastName": "Bondar", "phone": "12345", "specialization": "Yoga" }""")]
    [InlineData("""{ "firstName": "Taras", "lastName": "Bondar", "phone": "+380501112233", "email": "nope", "specialization": "Yoga" }""")]
    [InlineData("""{ "firstName": "Not json" """)]
    public async Task Hire_with_invalid_body_returns_400_problem_details(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await Admin.PostAsync(BaseUrl, content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Hire_with_specialization_longer_than_100_chars_returns_400()
    {
        var response = await Admin.PostAsJsonAsync(
            BaseUrl, new { firstName = "Taras", lastName = "Bondar", phone = UniquePhone(), specialization = new string('a', 101) }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Hire_with_duplicate_phone_in_another_format_returns_409()
    {
        var existing = await HireAsync();
        var reformatted = $"{existing.Phone[..4]} ({existing.Phone[4..6]}) {existing.Phone[6..9]}-{existing.Phone[9..]}";

        var response = await Admin.PostAsJsonAsync(BaseUrl, NewTrainer(phone: reformatted), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_as_receptionist_returns_trainer()
    {
        var created = await HireAsync();

        var trainer = await Receptionist.GetFromJsonAsync<TrainerResponse>($"{BaseUrl}/{created.Id}", Ct);

        Assert.Equivalent(created, trainer, strict: true);
    }

    [Fact]
    public async Task Get_missing_trainer_returns_404_problem_details()
    {
        var response = await Admin.GetAsync($"{BaseUrl}/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Update_as_admin_returns_updated_trainer()
    {
        var created = await HireAsync();

        var response = await Admin.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            firstName = "Ivan",
            lastName = "Koval",
            middleName = "Petrovych",
            phone = created.Phone,
            email = "ivan@club.com",
            specialization = "Pilates",
        }, Ct);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TrainerResponse>(Ct);
        Assert.NotNull(updated);
        Assert.Equal("Koval Ivan Petrovych", updated.FullName);
        Assert.Equal("ivan@club.com", updated.Email);
        Assert.Equal("Pilates", updated.Specialization);
        Assert.Equal(created.Phone, updated.Phone);
    }

    [Fact]
    public async Task Update_to_another_trainers_phone_returns_409()
    {
        var first = await HireAsync();
        var second = await HireAsync();

        var response = await Admin.PutAsJsonAsync($"{BaseUrl}/{second.Id}", NewTrainer(phone: first.Phone), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_missing_trainer_returns_404()
    {
        var response = await Admin.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", NewTrainer(), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_as_receptionist_returns_403()
    {
        var created = await HireAsync();

        var response = await Receptionist.PutAsJsonAsync($"{BaseUrl}/{created.Id}", NewTrainer(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_trainer_is_hidden_from_default_list_and_back_after_activate()
    {
        var trainer = await HireAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await Admin.PostAsync($"{BaseUrl}/{trainer.Id}/deactivate", null, Ct)).StatusCode);
        var activeOnly = await Receptionist.GetFromJsonAsync<List<TrainerSummaryResponse>>(BaseUrl, Ct);
        var all = await Receptionist.GetFromJsonAsync<List<TrainerSummaryResponse>>($"{BaseUrl}?includeInactive=true", Ct);
        Assert.DoesNotContain(activeOnly!, t => t.Id == trainer.Id);
        Assert.Contains(all!, t => t.Id == trainer.Id && !t.IsActive && t.Phone == trainer.Phone && t.Specialization == "Yoga");

        Assert.Equal(HttpStatusCode.NoContent, (await Admin.PostAsync($"{BaseUrl}/{trainer.Id}/activate", null, Ct)).StatusCode);
        var afterActivate = await Admin.GetFromJsonAsync<List<TrainerSummaryResponse>>(BaseUrl, Ct);
        Assert.Contains(afterActivate!, t => t.Id == trainer.Id && t.IsActive);
    }

    [Fact]
    public async Task Deactivate_missing_trainer_returns_404()
    {
        var response = await Admin.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/deactivate", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Activate_as_receptionist_returns_403()
    {
        var trainer = await HireAsync();

        var response = await Receptionist.PostAsync($"{BaseUrl}/{trainer.Id}/activate", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Set_working_hours_returns_sorted_hours_and_accepts_day_names_and_numbers()
    {
        var trainer = await HireAsync();
        using var content = new StringContent(
            """
            [
              { "day": "Wednesday", "start": "14:00:00", "end": "20:00:00" },
              { "day": 1, "start": "08:00:00", "end": "12:00:00" }
            ]
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await Admin.PutAsync($"{BaseUrl}/{trainer.Id}/working-hours", content, Ct);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("\"day\":\"Monday\"", json);
        var updated = await GetAsync(trainer.Id);
        Assert.Equal(
            [new WorkingHoursResponse(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
                new WorkingHoursResponse(DayOfWeek.Wednesday, new TimeOnly(14, 0), new TimeOnly(20, 0))],
            updated!.WorkingHours);
    }

    [Theory]
    [InlineData("""[{ "day": "Monday", "start": "09:00:00", "end": "13:00:00" }, { "day": "Monday", "start": "12:00:00", "end": "18:00:00" }]""")]
    [InlineData("""[{ "day": "Monday", "start": "18:00:00", "end": "09:00:00" }]""")]
    [InlineData("""[{ "day": "Funday", "start": "09:00:00", "end": "13:00:00" }]""")]
    [InlineData("""[{ "day": 9, "start": "09:00:00", "end": "13:00:00" }]""")]
    [InlineData("""[{ "start": "09:00:00", "end": "13:00:00" }]""")]
    [InlineData("""[{ "day": "Monday", "end": "13:00:00" }]""")]
    [InlineData("""{ "day": "Monday", "start": "09:00:00", "end": "13:00:00" }""")]
    [InlineData("""[null]""")]
    [InlineData("""null""")]
    public async Task Set_working_hours_with_invalid_body_returns_400(string json)
    {
        var trainer = await HireAsync();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await Admin.PutAsync($"{BaseUrl}/{trainer.Id}/working-hours", content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Set_working_hours_for_missing_trainer_returns_404()
    {
        var response = await Admin.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}/working-hours", Array.Empty<object>(), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Set_working_hours_as_receptionist_returns_403()
    {
        var trainer = await HireAsync();

        var response = await Receptionist.PutAsJsonAsync($"{BaseUrl}/{trainer.Id}/working-hours", Array.Empty<object>(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Assign_and_unassign_client_updates_client_list()
    {
        var trainer = await HireAsync();
        var clientId = await SeedClientAsync();

        var assign = await Admin.PostAsync($"{BaseUrl}/{trainer.Id}/clients/{clientId}", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        var assigned = Assert.Single((await GetAsync(trainer.Id))!.Clients);
        Assert.Equal(clientId, assigned.ClientId);
        Assert.Equal("Shevchenko Olena", assigned.FullName);

        var unassign = await Admin.DeleteAsync($"{BaseUrl}/{trainer.Id}/clients/{clientId}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, unassign.StatusCode);
        Assert.Empty((await GetAsync(trainer.Id))!.Clients);
    }

    [Fact]
    public async Task Assign_missing_client_returns_404()
    {
        var trainer = await HireAsync();

        var response = await Admin.PostAsync($"{BaseUrl}/{trainer.Id}/clients/{Guid.NewGuid()}", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Assign_client_to_missing_trainer_returns_404()
    {
        var clientId = await SeedClientAsync();

        var response = await Admin.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/clients/{clientId}", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Assign_client_twice_returns_400()
    {
        var trainer = await HireAsync();
        var clientId = await SeedClientAsync();
        await Admin.PostAsync($"{BaseUrl}/{trainer.Id}/clients/{clientId}", null, Ct);

        var response = await Admin.PostAsync($"{BaseUrl}/{trainer.Id}/clients/{clientId}", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("The client is already assigned to this trainer.", problem?.Detail);
    }

    [Fact]
    public async Task Assign_client_to_inactive_trainer_returns_400()
    {
        var trainer = await HireAsync();
        var clientId = await SeedClientAsync();
        await Admin.PostAsync($"{BaseUrl}/{trainer.Id}/deactivate", null, Ct);

        var response = await Admin.PostAsync($"{BaseUrl}/{trainer.Id}/clients/{clientId}", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unassign_client_that_is_not_assigned_returns_400()
    {
        var trainer = await HireAsync();

        var response = await Admin.DeleteAsync($"{BaseUrl}/{trainer.Id}/clients/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Assign_client_as_receptionist_returns_403()
    {
        var trainer = await HireAsync();
        var clientId = await SeedClientAsync();

        var response = await Receptionist.PostAsync($"{BaseUrl}/{trainer.Id}/clients/{clientId}", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unassign_client_as_receptionist_returns_403()
    {
        var trainer = await HireAsync();

        var response = await Receptionist.DeleteAsync($"{BaseUrl}/{trainer.Id}/clients/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Link_identity_returns_204_and_shows_in_detail()
    {
        var trainer = await HireAsync();
        var identity = $"auth0|{Guid.NewGuid():N}";

        var response = await Admin.PutAsJsonAsync($"{BaseUrl}/{trainer.Id}/identity", new { identityUserId = identity }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(identity, (await GetAsync(trainer.Id))!.IdentityUserId);
    }

    [Fact]
    public async Task Link_identity_of_another_trainer_returns_409()
    {
        var first = await HireAsync();
        var second = await HireAsync();
        var identity = $"auth0|{Guid.NewGuid():N}";
        await Admin.PutAsJsonAsync($"{BaseUrl}/{first.Id}/identity", new { identityUserId = identity }, Ct);

        var response = await Admin.PutAsJsonAsync($"{BaseUrl}/{second.Id}/identity", new { identityUserId = identity }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "identityUserId": "" }""")]
    [InlineData("""{ "identityUserId": "   " }""")]
    [InlineData("""{ }""")]
    public async Task Link_identity_with_invalid_body_returns_400(string json)
    {
        var trainer = await HireAsync();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await Admin.PutAsync($"{BaseUrl}/{trainer.Id}/identity", content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Link_identity_longer_than_128_chars_returns_400()
    {
        var trainer = await HireAsync();

        var response = await Admin.PutAsJsonAsync($"{BaseUrl}/{trainer.Id}/identity", new { identityUserId = new string('a', 129) }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Link_identity_for_missing_trainer_returns_404()
    {
        var response = await Admin.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}/identity", new { identityUserId = "auth0|x" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Link_identity_as_receptionist_returns_403()
    {
        var trainer = await HireAsync();

        var response = await Receptionist.PutAsJsonAsync($"{BaseUrl}/{trainer.Id}/identity", new { identityUserId = "auth0|x" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
