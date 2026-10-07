using System.Text.Json;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace FitnessClub.IntegrationTests.OpenApi;

public class OpenApiDocumentTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private static readonly string[] HttpMethods = ["get", "post", "put", "delete", "patch"];

    [Fact]
    public async Task Every_operation_has_a_controller_action_operation_id()
    {
        var operations = await Operations();

        Assert.NotEmpty(operations);
        Assert.All(operations, operation => Assert.Matches("^[A-Z][A-Za-z]+_[A-Z][A-Za-z]+$", operation.GetProperty("operationId").GetString()));
        Assert.Contains(operations, operation => operation.GetProperty("operationId").GetString() == "Clients_List");
    }

    [Fact]
    public async Task Numbers_are_not_typed_as_strings()
    {
        using var document = JsonDocument.Parse(await DocumentText());

        var numberOrStringTypes = TypeArrays(document.RootElement)
            .Where(types => types.Contains("string") && (types.Contains("integer") || types.Contains("number")))
            .ToList();

        Assert.Empty(numberOrStringTypes);
    }

    [Theory]
    [InlineData("/api/clients", "post", "201")]
    [InlineData("/api/rooms/{id}/activate", "post", "204")]
    [InlineData("/api/clients", "get", "200")]
    public async Task Success_status_codes_match_the_actions(string path, string method, string status)
    {
        using var document = JsonDocument.Parse(await DocumentText());

        var responses = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses");

        Assert.True(responses.TryGetProperty(status, out _));
    }

    private async Task<List<JsonElement>> Operations()
    {
        var document = JsonDocument.Parse(await DocumentText());
        return document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Where(operation => HttpMethods.Contains(operation.Name))
            .Select(operation => operation.Value)
            .ToList();
    }

    private static IEnumerable<string[]> TypeArrays(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().SelectMany(TypeArrays);

        if (element.ValueKind != JsonValueKind.Object)
            return [];

        var own = element.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Array
            ? [type.EnumerateArray().Select(item => item.GetString() ?? "").ToArray()]
            : Array.Empty<string[]>();

        return own.Concat(element.EnumerateObject().SelectMany(property => TypeArrays(property.Value)));
    }

    private async Task<string> DocumentText()
    {
        using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var response = await development.CreateClient().GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
