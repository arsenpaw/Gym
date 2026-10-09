using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Clients;
using FitnessClub.Application.Common;
using FitnessClub.Application.MembershipPlans;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.IntegrationTests.Clients;

public class ClientsEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private const string BaseUrl = "/api/clients";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    private HttpClient Receptionist() => factory.CreateClientWithRoles(Roles.Receptionist);

    private static string UniquePhone() => $"+380{Random.Shared.NextInt64(100_000_000, 1_000_000_000)}";

    private static string UniqueEmail() => $"client-{Guid.NewGuid():N}@example.com";

    private static object NewClient(string? email = null, string firstName = "Olena", string? phone = null, bool withPhone = true) =>
        new
        {
            firstName,
            lastName = "Shevchenko",
            dateOfBirth = "1995-03-14",
            email = email ?? UniqueEmail(),
            phone = withPhone ? phone ?? UniquePhone() : null,
        };

    private async Task<ClientDetailsResponse> RegisterAsync(object? body = null)
    {
        var response = await Receptionist().PostAsJsonAsync(BaseUrl, body ?? NewClient(), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientDetailsResponse>(Ct))!;
    }

    private async Task<MembershipPlanResponse> CreatePlanAsync(int validityDays = 30, int? visitLimit = null)
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(
            "/api/membership-plans", new { name = $"Plan {Guid.NewGuid():N}", price = 800m, validityDays, visitLimit }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MembershipPlanResponse>(Ct))!;
    }

    private async Task<MembershipResponse> PurchaseAsync(Guid clientId, int? visitLimit = null)
    {
        var plan = await CreatePlanAsync(visitLimit: visitLimit);
        var response = await Receptionist().PostAsJsonAsync(
            $"{BaseUrl}/{clientId}/memberships", new { planId = plan.Id, paymentMethod = "Cash" }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MembershipResponse>(Ct))!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string? detail = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        if (detail is not null)
            Assert.Equal(detail, (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Detail);
    }

    [Fact]
    public async Task Register_as_receptionist_returns_201_with_location()
    {
        var response = await Receptionist().PostAsJsonAsync(BaseUrl, NewClient(email: "Olena@Example.com"), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var client = await response.Content.ReadFromJsonAsync<ClientDetailsResponse>(Ct);
        Assert.NotNull(client);
        Assert.Equal("Shevchenko Olena", client.FullName);
        Assert.Equal("olena@example.com", client.Email);
        Assert.Equal(new DateOnly(1995, 3, 14), client.DateOfBirth);
        Assert.InRange(client.Age, 30, 32);
        Assert.Empty(client.Memberships);
        Assert.Equal($"{BaseUrl}/{client.Id}", response.Headers.Location?.AbsolutePath, ignoreCase: true);
    }

    [Fact]
    public async Task Register_as_admin_returns_201()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, NewClient(), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_without_user_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(BaseUrl, NewClient(), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_as_trainer_returns_403()
    {
        var response = await factory.CreateClientWithRoles(Roles.Trainer).PostAsJsonAsync(BaseUrl, NewClient(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/clients")]
    [InlineData("/api/clients/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/clients/00000000-0000-0000-0000-000000000001/visits")]
    public async Task Reads_as_trainer_return_403(string url)
    {
        var response = await factory.CreateClientWithRoles(Roles.Trainer).GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Check_in_as_trainer_returns_403()
    {
        var client = await RegisterAsync();

        var response = await factory.CreateClientWithRoles(Roles.Trainer).PostAsync($"{BaseUrl}/{client.Id}/visits", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "firstName": "", "lastName": "Shevchenko", "dateOfBirth": "1995-03-14", "email": "olena@example.com" }""")]
    [InlineData("""{ "firstName": "Olena", "lastName": "Shevchenko", "email": "olena@example.com" }""")]
    [InlineData("""{ "firstName": "Olena", "lastName": "Shevchenko", "dateOfBirth": "1995-03-14", "phone": "+380671234567" }""")]
    [InlineData("""{ "firstName": "Olena", "lastName": "Shevchenko", "dateOfBirth": "14.03.1995", "email": "olena@example.com" }""")]
    [InlineData("""{ "firstName": "Olena" """)]
    public async Task Register_with_invalid_body_returns_400_problem_details(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await Receptionist().PostAsync(BaseUrl, content, Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_with_invalid_phone_returns_400_from_domain_rule()
    {
        var response = await Receptionist().PostAsJsonAsync(BaseUrl, NewClient(phone: "12345"), Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Phone number must have 10 to 15 digits.");
    }

    [Fact]
    public async Task Register_with_invalid_email_returns_400_from_domain_rule()
    {
        var response = await Receptionist().PostAsJsonAsync(BaseUrl, NewClient(email: "not-an-email"), Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Email is not valid.");
    }

    [Fact]
    public async Task Register_with_existing_email_returns_409()
    {
        var existing = await RegisterAsync();

        var response = await Receptionist().PostAsJsonAsync(BaseUrl, NewClient(email: existing.Email.ToUpperInvariant()), Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, $"A client with email '{existing.Email}' already exists.");
    }

    [Fact]
    public async Task Register_without_phone_returns_201_with_no_phone()
    {
        var response = await Receptionist().PostAsJsonAsync(BaseUrl, NewClient(withPhone: false), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var client = await response.Content.ReadFromJsonAsync<ClientDetailsResponse>(Ct);
        Assert.Null(client!.Phone);
    }

    [Fact]
    public async Task Register_with_another_clients_phone_returns_201()
    {
        var existing = await RegisterAsync();

        var response = await Receptionist().PostAsJsonAsync(BaseUrl, NewClient(phone: existing.Phone), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_client_details()
    {
        var created = await RegisterAsync();

        var client = await Receptionist().GetFromJsonAsync<ClientDetailsResponse>($"{BaseUrl}/{created.Id}", Ct);

        Assert.NotNull(client);
        Assert.Equal(created.Id, client.Id);
        Assert.Equal(created.Email, client.Email);
        Assert.Equal(created.Phone, client.Phone);
    }

    [Fact]
    public async Task Get_missing_client_returns_404_problem_details()
    {
        var response = await Receptionist().GetAsync($"{BaseUrl}/{Guid.NewGuid()}", Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_returns_updated_client()
    {
        var created = await RegisterAsync();
        var phone = UniquePhone();

        var response = await Receptionist().PutAsJsonAsync($"{BaseUrl}/{created.Id}", NewClient(phone: phone, firstName: "Oksana"), Ct);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<ClientDetailsResponse>(Ct);
        Assert.Equal("Shevchenko Oksana", updated?.FullName);
        Assert.Equal(phone, updated?.Phone);
    }

    [Fact]
    public async Task Update_to_another_clients_email_returns_409()
    {
        var first = await RegisterAsync();
        var second = await RegisterAsync();

        var response = await Receptionist().PutAsJsonAsync($"{BaseUrl}/{second.Id}", NewClient(email: first.Email), Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_missing_client_returns_404()
    {
        var response = await Receptionist().PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", NewClient(), Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Purchase_membership_returns_201_and_shows_in_details_and_list()
    {
        var client = await RegisterAsync();
        var plan = await CreatePlanAsync(validityDays: 30, visitLimit: 12);

        var response = await Receptionist().PostAsJsonAsync(
            $"{BaseUrl}/{client.Id}/memberships", new { planId = plan.Id, paymentMethod = "Card" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{BaseUrl}/{client.Id}", response.Headers.Location?.AbsolutePath, ignoreCase: true);
        var membership = await response.Content.ReadFromJsonAsync<MembershipResponse>(Ct);
        Assert.NotNull(membership);
        Assert.Equal(plan.Name, membership.PlanName);
        Assert.Equal(800m, membership.Price);
        Assert.Equal(Today, membership.StartsOn);
        Assert.Equal(Today.AddDays(29), membership.EndsOn);
        Assert.Equal(12, membership.VisitLimit);
        Assert.Equal(0, membership.VisitsUsed);
        Assert.True(membership.IsActive);

        var details = await Receptionist().GetFromJsonAsync<ClientDetailsResponse>($"{BaseUrl}/{client.Id}", Ct);
        Assert.Equal(membership, Assert.Single(details!.Memberships));
        Assert.Equal(membership.Id, details.ActiveMembership?.Id);

        var list = await Receptionist().GetFromJsonAsync<List<ClientSummaryResponse>>(BaseUrl, Ct);
        var summary = Assert.Single(list!, c => c.Id == client.Id);
        Assert.Equal(new ActiveMembershipResponse(membership.Id, plan.Name, Today, Today.AddDays(29), 12), summary.ActiveMembership);
    }

    [Fact]
    public async Task Purchase_membership_starting_later_is_not_active_yet()
    {
        var client = await RegisterAsync();
        var plan = await CreatePlanAsync();

        var response = await Receptionist().PostAsJsonAsync(
            $"{BaseUrl}/{client.Id}/memberships", new { planId = plan.Id, startsOn = Today.AddDays(7), paymentMethod = "Cash" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var membership = await response.Content.ReadFromJsonAsync<MembershipResponse>(Ct);
        Assert.Equal(Today.AddDays(7), membership?.StartsOn);
        Assert.False(membership?.IsActive);
    }

    [Fact]
    public async Task Purchase_membership_for_missing_plan_returns_404()
    {
        var client = await RegisterAsync();

        var response = await Receptionist().PostAsJsonAsync(
            $"{BaseUrl}/{client.Id}/memberships", new { planId = Guid.NewGuid(), paymentMethod = "Cash" }, Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Purchase_membership_for_missing_client_returns_404()
    {
        var plan = await CreatePlanAsync();

        var response = await Receptionist().PostAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}/memberships", new { planId = plan.Id, paymentMethod = "Cash" }, Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Purchase_membership_starting_in_the_past_returns_400()
    {
        var client = await RegisterAsync();
        var plan = await CreatePlanAsync();

        var response = await Receptionist().PostAsJsonAsync(
            $"{BaseUrl}/{client.Id}/memberships", new { planId = plan.Id, startsOn = Today.AddDays(-1), paymentMethod = "Cash" }, Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "A membership cannot start in the past.");
    }

    [Fact]
    public async Task Purchase_membership_overlapping_an_active_one_returns_400()
    {
        var client = await RegisterAsync();
        await PurchaseAsync(client.Id);
        var plan = await CreatePlanAsync();

        var response = await Receptionist().PostAsJsonAsync(
            $"{BaseUrl}/{client.Id}/memberships", new { planId = plan.Id, paymentMethod = "Cash" }, Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "The client already has a membership for these dates.");
    }

    [Theory]
    [InlineData("""{ "paymentMethod": "Cash" }""")]
    [InlineData("""{ "planId": "00000000-0000-0000-0000-000000000001" }""")]
    [InlineData("""{ "planId": "00000000-0000-0000-0000-000000000001", "paymentMethod": "Bitcoin" }""")]
    [InlineData("""{ "planId": "not-a-guid", "paymentMethod": "Cash" }""")]
    public async Task Purchase_membership_with_invalid_body_returns_400(string json)
    {
        var client = await RegisterAsync();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await Receptionist().PostAsync($"{BaseUrl}/{client.Id}/memberships", content, Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_membership_returns_204_and_a_second_cancel_returns_400()
    {
        var client = await RegisterAsync();
        var membership = await PurchaseAsync(client.Id);
        var url = $"{BaseUrl}/{client.Id}/memberships/{membership.Id}/cancel";

        var response = await Receptionist().PostAsync(url, null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var details = await Receptionist().GetFromJsonAsync<ClientDetailsResponse>($"{BaseUrl}/{client.Id}", Ct);
        Assert.Null(details!.ActiveMembership);
        Assert.NotNull(Assert.Single(details.Memberships).CancelledAt);

        await AssertProblemAsync(await Receptionist().PostAsync(url, null, Ct), HttpStatusCode.BadRequest, "The membership is already cancelled.");
    }

    [Fact]
    public async Task Cancel_unknown_membership_returns_404()
    {
        var client = await RegisterAsync();

        var response = await Receptionist().PostAsync($"{BaseUrl}/{client.Id}/memberships/{Guid.NewGuid()}/cancel", null, Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cancel_another_clients_membership_returns_404()
    {
        var owner = await RegisterAsync();
        var other = await RegisterAsync();
        var membership = await PurchaseAsync(owner.Id);

        var response = await Receptionist().PostAsync($"{BaseUrl}/{other.Id}/memberships/{membership.Id}/cancel", null, Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Check_in_returns_201_records_visit_and_uses_one_visit()
    {
        var client = await RegisterAsync();
        var membership = await PurchaseAsync(client.Id, visitLimit: 10);

        var response = await Receptionist().PostAsync($"{BaseUrl}/{client.Id}/visits", null, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{BaseUrl}/{client.Id}/visits", response.Headers.Location?.AbsolutePath, ignoreCase: true);
        var visit = await response.Content.ReadFromJsonAsync<VisitResponse>(Ct);
        Assert.NotNull(visit);
        Assert.Equal(client.Id, visit.ClientId);
        Assert.Equal(membership.Id, visit.MembershipId);

        var history = await Receptionist().GetFromJsonAsync<VisitPageResponse>($"{BaseUrl}/{client.Id}/visits", Ct);
        Assert.Equal(visit, Assert.Single(history!.Items));
        Assert.Equal(1, history.TotalCount);

        var details = await Receptionist().GetFromJsonAsync<ClientDetailsResponse>($"{BaseUrl}/{client.Id}", Ct);
        Assert.Equal(9, details!.ActiveMembership?.VisitsLeft);
        Assert.Equal(1, Assert.Single(details.Memberships).VisitsUsed);
    }

    [Fact]
    public async Task Second_check_in_on_the_same_day_returns_400_and_records_nothing()
    {
        var client = await RegisterAsync();
        await PurchaseAsync(client.Id);
        await Receptionist().PostAsync($"{BaseUrl}/{client.Id}/visits", null, Ct);

        var response = await Receptionist().PostAsync($"{BaseUrl}/{client.Id}/visits", null, Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "The client has already checked in today.");
        var history = await Receptionist().GetFromJsonAsync<VisitPageResponse>($"{BaseUrl}/{client.Id}/visits", Ct);
        Assert.Single(history!.Items);
    }

    [Fact]
    public async Task Check_in_without_membership_returns_400()
    {
        var client = await RegisterAsync();

        var response = await Receptionist().PostAsync($"{BaseUrl}/{client.Id}/visits", null, Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "The client has no active membership today.");
    }

    [Fact]
    public async Task Check_in_for_missing_client_returns_404()
    {
        var response = await Receptionist().PostAsync($"{BaseUrl}/{Guid.NewGuid()}/visits", null, Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Visits_for_new_client_are_empty()
    {
        var client = await RegisterAsync();

        var history = await Receptionist().GetFromJsonAsync<VisitPageResponse>($"{BaseUrl}/{client.Id}/visits?page=1&pageSize=25", Ct);

        Assert.Empty(history!.Items);
        Assert.Equal(0, history.TotalCount);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Visits_with_invalid_paging_return_400(string query)
    {
        var client = await RegisterAsync();

        var response = await Receptionist().GetAsync($"{BaseUrl}/{client.Id}/visits?{query}", Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Visits_for_missing_client_return_404()
    {
        var response = await Receptionist().GetAsync($"{BaseUrl}/{Guid.NewGuid()}/visits", Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }
}
