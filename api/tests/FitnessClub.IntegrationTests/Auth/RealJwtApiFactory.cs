using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FitnessClub.IntegrationTests.Auth;

// Keeps the real JWT bearer setup from AuthenticationSetup; only the Auth0 metadata download
// is replaced by a local issuer and signing key, so tokens can be minted in-process.
public sealed class RealJwtApiFactory : WebApplicationFactory<Program>
{
    public const string Domain = "test.invalid";
    public const string Issuer = $"https://{Domain}/";
    public const string Audience = "https://api.test";

    private static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth0:Domain", Domain);
        builder.UseSetting("Auth0:Audience", Audience);
        builder.UseSetting("Database:InMemoryName", $"jwt-{Guid.NewGuid()}");

        builder.ConfigureTestServices(services =>
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(SigningKey);
                options.Configuration = configuration;
            }));
    }

    public HttpClient CreateClientWithToken(IDictionary<string, object> claims, string audience = Audience)
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = new Dictionary<string, object>(claims) { ["sub"] = "auth0|test-user" },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        });

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}
