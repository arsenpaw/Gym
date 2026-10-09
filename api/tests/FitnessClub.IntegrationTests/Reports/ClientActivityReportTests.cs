using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;
using FitnessClub.Application.Reports;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Visits;
using Microsoft.Extensions.DependencyInjection;
using static FitnessClub.IntegrationTests.Reports.ReportsApiFixture;

namespace FitnessClub.IntegrationTests.Reports;

public class ClientActivityReportTests(ReportsApiFixture fixture) : IClassFixture<ReportsApiFixture>
{
    private const string Url = "/api/reports/client-activity";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<ClientActivityReportResponse?> GetReportAsync(string query = "") =>
        fixture.CreateClientWithRoles(Roles.Admin).GetFromJsonAsync<ClientActivityReportResponse>($"{Url}{query}", Ct);

    private Task SeedAsync(IReadOnlyList<MembershipPlan> plans, IReadOnlyList<Client> clients, IReadOnlyList<Visit> visits) =>
        fixture.InUnitOfWorkAsync(services =>
        {
            var planRepository = services.GetRequiredService<IMembershipPlanRepository>();
            var clientRepository = services.GetRequiredService<IClientRepository>();
            var visitRepository = services.GetRequiredService<IVisitRepository>();
            foreach (var plan in plans)
                planRepository.Add(plan);
            foreach (var client in clients)
                clientRepository.Add(client);
            foreach (var visit in visits)
                visitRepository.Add(visit);
        });

    [Fact]
    public async Task Lists_every_client_with_age_active_membership_and_visits_in_range_ordered_by_name()
    {
        var longPlan = NewPlan(validityDays: 60);
        var shortPlan = NewPlan(validityDays: 30);

        var active = NewClient("Avramenko", "Anna", new DateOnly(1990, 10, 8), Local(2026, 8, 1));
        active.PurchaseMembership(longPlan, new DateOnly(2026, 8, 25), PaymentMethod.Card, Local(2026, 8, 25, 9));
        List<Visit> visits =
        [
            active.CheckIn(Local(2026, 8, 30)),
            active.CheckIn(Local(2026, 9, 5)),
            active.CheckIn(Local(2026, 9, 30, 23)),
            active.CheckIn(Local(2026, 10, 1, 0, 30)),
            active.CheckIn(Local(2026, 10, 2, 18)),
        ];

        var idle = NewClient("Bilyk", "Bohdan", new DateOnly(2000, 10, 7), Local(2026, 9, 1));

        var expired = NewClient("Chorna", "Chrystyna", new DateOnly(1985, 1, 1), Local(2026, 1, 10));
        expired.PurchaseMembership(shortPlan, new DateOnly(2026, 1, 10), PaymentMethod.Cash, Local(2026, 1, 10, 9));
        visits.Add(expired.CheckIn(Local(2026, 1, 15)));

        await SeedAsync([longPlan, shortPlan], [expired, idle, active], visits);

        var report = await GetReportAsync("?from=2026-09-01&to=2026-09-30");

        Assert.NotNull(report);
        Assert.Equal(new DateOnly(2026, 9, 1), report.From);
        Assert.Equal(new DateOnly(2026, 9, 30), report.To);

        Guid[] seeded = [active.Id, idle.Id, expired.Id];
        var rows = report.Clients.Where(row => seeded.Contains(row.ClientId)).ToList();
        Assert.Equal(seeded, rows.Select(row => row.ClientId));

        var activeRow = rows[0];
        Assert.Equal("Avramenko Anna", activeRow.FullName);
        Assert.Equal(35, activeRow.Age);
        Assert.Equal(active.Email.Value, activeRow.Email);
        Assert.Equal(active.Phone!.Value, activeRow.Phone);
        Assert.NotNull(activeRow.ActiveMembership);
        Assert.Equal(longPlan.Name, activeRow.ActiveMembership.PlanName);
        Assert.Equal(new DateOnly(2026, 8, 25), activeRow.ActiveMembership.StartsOn);
        Assert.Equal(new DateOnly(2026, 10, 23), activeRow.ActiveMembership.EndsOn);
        Assert.Null(activeRow.ActiveMembership.RemainingVisits);
        Assert.Equal(2, activeRow.VisitCount);
        Assert.Equal(Local(2026, 10, 2, 18), activeRow.LastVisitAt);

        var idleRow = rows[1];
        Assert.Equal(26, idleRow.Age);
        Assert.Null(idleRow.ActiveMembership);
        Assert.Equal(0, idleRow.VisitCount);
        Assert.Null(idleRow.LastVisitAt);

        var expiredRow = rows[2];
        Assert.Equal(41, expiredRow.Age);
        Assert.Null(expiredRow.ActiveMembership);
        Assert.Equal(0, expiredRow.VisitCount);
        Assert.Equal(Local(2026, 1, 15), expiredRow.LastVisitAt);
    }

    [Fact]
    public async Task Default_range_is_the_last_30_days_up_to_today()
    {
        var plan = NewPlan(validityDays: 60);
        var client = NewClient("Danylenko", "Dmytro", new DateOnly(1995, 5, 5), Local(2026, 9, 1));
        client.PurchaseMembership(plan, new DateOnly(2026, 9, 1), PaymentMethod.Cash, Local(2026, 9, 1, 9));
        Visit[] visits =
        [
            client.CheckIn(Local(2026, 9, 7, 23, 59)),
            client.CheckIn(Local(2026, 9, 8, 0, 15)),
            client.CheckIn(Local(2026, 10, 7, 9)),
        ];
        await SeedAsync([plan], [client], visits);

        var report = await GetReportAsync();

        Assert.NotNull(report);
        Assert.Equal(new DateOnly(2026, 9, 8), report.From);
        Assert.Equal(Today, report.To);
        var row = Assert.Single(report.Clients, row => row.ClientId == client.Id);
        Assert.Equal(2, row.VisitCount);
        Assert.Equal(Local(2026, 10, 7, 9), row.LastVisitAt);
    }

    [Theory]
    [InlineData("?from=2026-09-30&to=2026-09-01")]
    [InlineData("?from=2026-11-01")]
    [InlineData("?from=not-a-date")]
    [InlineData("?to=2026-13-01")]
    public async Task Invalid_range_returns_400_problem_details(string query)
    {
        var response = await fixture.CreateClientWithRoles(Roles.Admin).GetAsync($"{Url}{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
