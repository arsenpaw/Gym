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
            builder.UseSetting("ConnectionStrings:FitnessClub", SqlServerFixture.NewDatabaseConnectionString());
        });

        var exception = Assert.Throws<OptionsValidationException>(() => unconfigured.CreateClient());
        Assert.Contains("Domain", exception.Message);
        Assert.Contains("Audience", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_connection_string_fails_at_startup(string connectionString)
    {
        using var unconfigured = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Auth0:Domain", "test.invalid");
            builder.UseSetting("Auth0:Audience", "https://api.test");
            builder.UseSetting("ConnectionStrings:FitnessClub", connectionString);
        });

        var exception = Record.Exception(() => unconfigured.CreateClient());

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception.GetBaseException());
        Assert.Contains("ConnectionStrings:FitnessClub", exception.GetBaseException().Message);
    }

    [Fact]
    public void Invalid_expiry_notice_days_fail_at_startup()
    {
        using var baseFactory = new FitnessClubApiFactory();
        using var invalid = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Notifications:ExpiryNoticeDays", "0"));

        var exception = Assert.Throws<OptionsValidationException>(() => invalid.CreateClient());
        Assert.Contains("ExpiryNoticeDays", exception.Message);
    }
}
