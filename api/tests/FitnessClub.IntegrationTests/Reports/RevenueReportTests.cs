using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;
using FitnessClub.Application.Reports;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using Microsoft.Extensions.DependencyInjection;
using static FitnessClub.IntegrationTests.Reports.ReportsApiFixture;

namespace FitnessClub.IntegrationTests.Reports;

public class RevenueReportTests(ReportsApiFixture fixture) : IClassFixture<ReportsApiFixture>
{
    private const string Url = "/api/reports/revenue";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeSpan Utc = TimeSpan.Zero;

    private Task<RevenueReportResponse?> GetReportAsync(string query = "") =>
        fixture.CreateClientWithRoles(Roles.Admin).GetFromJsonAsync<RevenueReportResponse>($"{Url}{query}", Ct);

    private Task SeedPaymentsAsync(params (decimal Price, DateTimeOffset PaidAt)[] payments) =>
        fixture.InUnitOfWorkAsync(services =>
        {
            foreach (var (price, paidAt) in payments)
            {
                var plan = NewPlan(validityDays: 30, price);
                var client = NewClient("Payer", "Petro", new DateOnly(1990, 1, 1), paidAt);
                var payment = client.PurchaseMembership(plan, DateOnly.FromDateTime(paidAt.DateTime), PaymentMethod.Card, paidAt);
                services.GetRequiredService<IMembershipPlanRepository>().Add(plan);
                services.GetRequiredService<IClientRepository>().Add(client);
                services.GetRequiredService<IPaymentRepository>().Add(payment);
            }
        });

    [Fact]
    public async Task Yearly_report_sums_payments_per_club_local_month_with_all_months_zero_filled()
    {
        await SeedPaymentsAsync(
            (100m, new DateTimeOffset(2024, 1, 31, 22, 30, 0, Utc)),
            (250.50m, Local(2024, 3, 10, 12)),
            (300m, Local(2024, 3, 20, 12)),
            (999m, Local(2024, 12, 31, 23, 59)),
            (555m, Local(2025, 1, 1, 0, 0)),
            (777m, new DateTimeOffset(2023, 12, 31, 20, 59, 0, Utc)));

        var report = await GetReportAsync("?year=2024");

        Assert.NotNull(report);
        Assert.Equal(2024, report.Year);
        Assert.Null(report.Month);
        Assert.Equal(new DateOnly(2024, 1, 1), report.From);
        Assert.Equal(new DateOnly(2024, 12, 31), report.To);
        Assert.Equal(1649.50m, report.Total);
        Assert.Equal(4, report.PaymentCount);
        Assert.Equal(12, report.Breakdown.Count);
        Assert.Equal(new RevenueBucket(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), 0m, 0), report.Breakdown[0]);
        Assert.Equal(new RevenueBucket(new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 29), 100m, 1), report.Breakdown[1]);
        Assert.Equal(new RevenueBucket(new DateOnly(2024, 3, 1), new DateOnly(2024, 3, 31), 550.50m, 2), report.Breakdown[2]);
        Assert.Equal(new RevenueBucket(new DateOnly(2024, 12, 1), new DateOnly(2024, 12, 31), 999m, 1), report.Breakdown[11]);
        Assert.All(report.Breakdown.Skip(3).Take(8), bucket => Assert.Equal((0m, 0), (bucket.Total, bucket.PaymentCount)));
    }

    [Fact]
    public async Task Monthly_report_sums_payments_per_club_local_day_with_all_days_zero_filled()
    {
        await SeedPaymentsAsync(
            (50m, Local(2023, 5, 1, 0, 0)),
            (70m, new DateTimeOffset(2023, 5, 31, 20, 59, 0, Utc)),
            (20m, Local(2023, 5, 31, 10)),
            (999m, Local(2023, 6, 1, 0, 0)),
            (999m, new DateTimeOffset(2023, 4, 30, 20, 59, 0, Utc)));

        var report = await GetReportAsync("?year=2023&month=5");

        Assert.NotNull(report);
        Assert.Equal(5, report.Month);
        Assert.Equal(new DateOnly(2023, 5, 1), report.From);
        Assert.Equal(new DateOnly(2023, 5, 31), report.To);
        Assert.Equal(140m, report.Total);
        Assert.Equal(3, report.PaymentCount);
        Assert.Equal(31, report.Breakdown.Count);
        Assert.Equal(new RevenueBucket(new DateOnly(2023, 5, 1), new DateOnly(2023, 5, 1), 50m, 1), report.Breakdown[0]);
        Assert.Equal(new RevenueBucket(new DateOnly(2023, 5, 31), new DateOnly(2023, 5, 31), 90m, 2), report.Breakdown[30]);
        Assert.All(report.Breakdown.Skip(1).Take(29), bucket => Assert.Equal((0m, 0), (bucket.Total, bucket.PaymentCount)));
    }

    [Fact]
    public async Task Year_without_payments_returns_zero_totals()
    {
        var report = await GetReportAsync("?year=2030");

        Assert.NotNull(report);
        Assert.Equal(0m, report.Total);
        Assert.Equal(0, report.PaymentCount);
        Assert.Equal(12, report.Breakdown.Count);
    }

    [Fact]
    public async Task Default_is_the_current_club_year()
    {
        var report = await GetReportAsync();

        Assert.NotNull(report);
        Assert.Equal(Today.Year, report.Year);
        Assert.Null(report.Month);
        Assert.Equal(12, report.Breakdown.Count);
    }

    [Theory]
    [InlineData("?year=2026&month=0")]
    [InlineData("?year=2026&month=13")]
    [InlineData("?month=-1")]
    [InlineData("?year=1999")]
    [InlineData("?year=2101")]
    [InlineData("?year=abc")]
    public async Task Invalid_parameters_return_400_problem_details(string query)
    {
        var response = await fixture.CreateClientWithRoles(Roles.Admin).GetAsync($"{Url}{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
