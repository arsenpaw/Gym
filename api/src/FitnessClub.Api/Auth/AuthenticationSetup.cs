using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FitnessClub.Api.Auth;

public static class AuthenticationSetup
{
    public static IServiceCollection AddAuth0Authentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Auth0Options>()
            .Bind(configuration.GetSection(Auth0Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<Auth0Options>>((jwt, auth0Options) =>
            {
                var auth0 = auth0Options.Value;
                jwt.Authority = $"https://{auth0.Domain}/";
                jwt.Audience = auth0.Audience;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.NameClaimType = "sub";
                jwt.TokenValidationParameters.RoleClaimType = auth0.RolesClaim;
            });

        // Every endpoint requires a signed-in user unless it opts out with AllowAnonymous.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
