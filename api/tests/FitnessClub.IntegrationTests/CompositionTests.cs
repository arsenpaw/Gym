using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Training;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests;

public class CompositionTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private static List<Type> Contracts()
    {
        var repositories = typeof(AggregateRoot).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Name.StartsWith('I') && type.Name.EndsWith("Repository"));
        var services = typeof(IUnitOfWork).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.IsPublic && type.Name.StartsWith('I') && type.Name.EndsWith("Service"));

        return repositories
            .Concat(services)
            .Append(typeof(IUnitOfWork))
            .Append(typeof(ISessionScheduler))
            .ToList();
    }

    public static TheoryData<string> ScopedContracts() =>
        new(Contracts().Select(type => type.AssemblyQualifiedName!));

    [Theory]
    [MemberData(nameof(ScopedContracts))]
    public async Task Contract_resolves_from_a_request_scope(string contractName)
    {
        var contract = Type.GetType(contractName, throwOnError: true)!;
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService(contract));
    }

    [Fact]
    public void Contract_list_covers_every_repository_and_service()
    {
        var names = Contracts().Select(type => type.Name).ToList();

        Assert.Contains(nameof(IUnitOfWork), names);
        Assert.Contains(nameof(ISessionScheduler), names);
        Assert.Contains("IClientRepository", names);
        Assert.Contains("ITrainingSessionRepository", names);
        Assert.Contains("IMembershipPlanService", names);
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
