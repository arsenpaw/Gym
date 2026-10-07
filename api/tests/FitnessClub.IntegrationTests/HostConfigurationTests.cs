using System.Net;
using FitnessClub.Application.Common;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace FitnessClub.IntegrationTests;

public class HostConfigurationTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    [Theory]
    [InlineData("/hangfire")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    [InlineData("/swagger/index.html")]
    public async Task Development_only_endpoints_are_not_mapped_outside_development(string path)
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Missing_auth0_settings_fail_at_startup()
    {
        using var unconfigured = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Auth0:Domain", "");
            builder.UseSetting("Auth0:Audience", "");
        });

        var exception = Assert.Throws<OptionsValidationException>(() => unconfigured.CreateClient());
        Assert.Contains("Domain", exception.Message);
        Assert.Contains("Audience", exception.Message);
    }
}
