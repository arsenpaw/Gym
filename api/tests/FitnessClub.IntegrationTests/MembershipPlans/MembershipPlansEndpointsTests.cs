using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;
using FitnessClub.Application.MembershipPlans;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.IntegrationTests.MembershipPlans;

public class MembershipPlansEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private const string BaseUrl = "/api/membership-plans";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object NewPlan(decimal price = 800m, int validityDays = 30, int? visitLimit = null) =>
        new { name = $"Plan {Guid.NewGuid():N}", price, validityDays, visitLimit };

    private async Task<MembershipPlanResponse> CreatePlanAsync(object? body = null)
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, body ?? NewPlan(), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MembershipPlanResponse>(Ct))!;
    }

    [Fact]
    public async Task Create_as_admin_returns_201_with_location()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, NewPlan(visitLimit: 10), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var plan = await response.Content.ReadFromJsonAsync<MembershipPlanResponse>(Ct);
        Assert.NotNull(plan);
        Assert.Equal(10, plan.VisitLimit);
        Assert.True(plan.IsActive);
        Assert.Equal($"{BaseUrl}/{plan.Id}", response.Headers.Location?.AbsolutePath, ignoreCase: true);
    }

    [Fact]
    public async Task Create_without_user_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(BaseUrl, NewPlan(), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_as_receptionist_returns_403()
    {
        var response = await factory.CreateClientWithRoles(Roles.Receptionist).PostAsJsonAsync(BaseUrl, NewPlan(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_as_trainer_returns_403()
    {
        var response = await factory.CreateClientWithRoles(Roles.Trainer).GetAsync(BaseUrl, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "name": "", "price": 100, "validityDays": 30 }""")]
    [InlineData("""{ "name": "No price", "validityDays": 30 }""")]
    [InlineData("""{ "name": "Bad validity", "price": 100, "validityDays": 0 }""")]
    [InlineData("""{ "name": "Bad limit", "price": 100, "validityDays": 30, "visitLimit": 0 }""")]
    [InlineData("""{ "name": "   ", "price": 100, "validityDays": 30 }""")]
    [InlineData("""{ "name": "Text price", "price": "abc", "validityDays": 30 }""")]
    [InlineData("""{ "name": "Huge price", "price": 1e30, "validityDays": 30 }""")]
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
            .PostAsJsonAsync(BaseUrl, new { name = new string('a', 101), price = 100m, validityDays = 30 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_with_malformed_id_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{BaseUrl}/not-a-guid", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_more_than_two_decimals_returns_400_from_domain_rule()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, NewPlan(price: 10.001m), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("Price can have at most 2 decimal places.", problem?.Detail);
    }

    [Fact]
    public async Task Create_with_duplicate_name_returns_409()
    {
        var existing = await CreatePlanAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin)
            .PostAsJsonAsync(BaseUrl, new { name = existing.Name.ToUpperInvariant(), price = 100m, validityDays = 30 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Get_as_receptionist_returns_plan()
    {
        var created = await CreatePlanAsync();

        var plan = await factory.CreateClientWithRoles(Roles.Receptionist)
            .GetFromJsonAsync<MembershipPlanResponse>($"{BaseUrl}/{created.Id}", Ct);

        Assert.Equal(created, plan);
    }

    [Fact]
    public async Task Get_missing_plan_returns_404_problem_details()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{BaseUrl}/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Update_as_admin_returns_updated_plan()
    {
        var created = await CreatePlanAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin).PutAsJsonAsync(
            $"{BaseUrl}/{created.Id}", new { name = created.Name, price = 950.50m, validityDays = 60, visitLimit = 20 }, Ct);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MembershipPlanResponse>(Ct);
        Assert.Equal(created with { Price = 950.50m, ValidityDays = 60, VisitLimit = 20 }, updated);
    }

    [Fact]
    public async Task Update_missing_plan_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", NewPlan(), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_plan_is_hidden_from_default_list_and_back_after_activate()
    {
        var plan = await CreatePlanAsync();
        var admin = factory.CreateClientWithRoles(Roles.Admin);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"{BaseUrl}/{plan.Id}/deactivate", null, Ct)).StatusCode);
        var activeOnly = await admin.GetFromJsonAsync<List<MembershipPlanResponse>>(BaseUrl, Ct);
        var all = await admin.GetFromJsonAsync<List<MembershipPlanResponse>>($"{BaseUrl}?includeInactive=true", Ct);
        Assert.DoesNotContain(activeOnly!, p => p.Id == plan.Id);
        Assert.Contains(all!, p => p.Id == plan.Id && !p.IsActive);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"{BaseUrl}/{plan.Id}/activate", null, Ct)).StatusCode);
        var afterActivate = await admin.GetFromJsonAsync<List<MembershipPlanResponse>>(BaseUrl, Ct);
        Assert.Contains(afterActivate!, p => p.Id == plan.Id);
    }

    [Fact]
    public async Task Deactivate_missing_plan_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsync($"{BaseUrl}/{Guid.NewGuid()}/deactivate", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_as_receptionist_returns_403()
    {
        var plan = await CreatePlanAsync();

        var response = await factory.CreateClientWithRoles(Roles.Receptionist).PostAsync($"{BaseUrl}/{plan.Id}/deactivate", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
