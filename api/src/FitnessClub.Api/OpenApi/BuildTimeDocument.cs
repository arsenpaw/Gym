using System.Reflection;

namespace FitnessClub.Api.OpenApi;

internal static class BuildTimeDocument
{
    private const string GeneratorAssemblyName = "GetDocument.Insider";

    public static void AddPlaceholderSettingsWhenGenerating(ConfigurationManager configuration)
    {
        if (Assembly.GetEntryAssembly()?.GetName().Name != GeneratorAssemblyName)
            return;

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth0:Domain"] = "openapi.invalid",
            ["Auth0:Audience"] = "https://openapi.invalid"
        });
    }
}
