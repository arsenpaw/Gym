using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FitnessClub.Api.OpenApi;

internal static class BuildTimeDocument
{
    private const string GeneratorAssemblyName = "GetDocument.Insider";

    public static bool IsGenerating => Assembly.GetEntryAssembly()?.GetName().Name == GeneratorAssemblyName;

    public static void AddPlaceholderSettingsWhenGenerating(ConfigurationManager configuration)
    {
        if (!IsGenerating)
            return;

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth0:Domain"] = "openapi.invalid",
            ["Auth0:Audience"] = "https://openapi.invalid",
            ["ConnectionStrings:FitnessClub"] = "Server=openapi.invalid;Database=FitnessClub;TrustServerCertificate=True"
        });
    }

    public static void RemoveHostedServicesWhenGenerating(IServiceCollection services)
    {
        if (IsGenerating)
            services.RemoveAll<IHostedService>();
    }
}
