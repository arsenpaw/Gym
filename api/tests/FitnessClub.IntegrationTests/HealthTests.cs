using System.Net;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests;

public class HealthTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    [Fact]
    public async Task Health_is_public_and_returns_200()
    {
        var response = await factory.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
