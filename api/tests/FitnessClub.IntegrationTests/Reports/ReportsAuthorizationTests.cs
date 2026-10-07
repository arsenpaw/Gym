using System.Net;
using FitnessClub.Application.Common;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Reports;

public class ReportsAuthorizationTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> NonAdminRequests()
    {
        var data = new TheoryData<string, string>();
        foreach (var url in Urls)
            foreach (var role in new[] { Roles.Receptionist, Roles.Trainer })
                data.Add(url, role);
        return data;
    }

    public static TheoryData<string> AllUrls() => new(Urls);

    private static readonly string[] Urls =
    [
        "/api/reports/client-activity",
        "/api/reports/revenue",
        "/api/reports/load",
    ];

    [Theory]
    [MemberData(nameof(NonAdminRequests))]
    public async Task Non_admin_gets_403(string url, string role)
    {
        var response = await factory.CreateClientWithRoles(role).GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllUrls))]
    public async Task Anonymous_gets_401(string url)
    {
        var response = await factory.CreateClient().GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllUrls))]
    public async Task Admin_gets_200(string url)
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
