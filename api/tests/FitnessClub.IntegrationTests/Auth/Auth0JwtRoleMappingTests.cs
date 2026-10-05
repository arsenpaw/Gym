using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;

namespace FitnessClub.IntegrationTests.Auth;

// Exercises the real JWT bearer pipeline: roles must come from the Auth0 custom claim.
public class Auth0JwtRoleMappingTests(RealJwtApiFactory factory) : IClassFixture<RealJwtApiFactory>
{
    private const string BaseUrl = "/api/membership-plans";
    private const string RolesClaim = "https://fitnessclub/roles";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object NewPlan() => new { name = $"Plan {Guid.NewGuid():N}", price = 800m, validityDays = 30 };

    private static Dictionary<string, object> RolesInClaim(string claimType, params string[] roles) =>
        new() { [claimType] = roles };

    [Fact]
    public async Task Admin_role_in_auth0_roles_claim_can_create()
    {
        var client = factory.CreateClientWithToken(RolesInClaim(RolesClaim, Roles.Admin));

        var response = await client.PostAsJsonAsync(BaseUrl, NewPlan(), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Receptionist_role_in_auth0_roles_claim_can_list_but_not_create()
    {
        var client = factory.CreateClientWithToken(RolesInClaim(RolesClaim, Roles.Receptionist));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(BaseUrl, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(BaseUrl, NewPlan(), Ct)).StatusCode);
    }

    [Fact]
    public async Task Role_in_plain_roles_claim_is_ignored()
    {
        var client = factory.CreateClientWithToken(RolesInClaim("roles", Roles.Admin));

        var response = await client.PostAsJsonAsync(BaseUrl, NewPlan(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Token_for_another_audience_is_rejected()
    {
        var client = factory.CreateClientWithToken(RolesInClaim(RolesClaim, Roles.Admin), audience: "https://other-api");

        var response = await client.GetAsync(BaseUrl, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
