using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Services;

public abstract class ServiceTestBase : IClassFixture<FitnessClubApiFactory>, IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;
    private readonly IUnitOfWork _seedUnitOfWork;

    protected ServiceTestBase(FitnessClubApiFactory factory)
    {
        var services = factory.Services;
        TestDatabase.DeleteAllRows(factory.ConnectionString);
        _scope = services.CreateAsyncScope();
        _seedUnitOfWork = Get<IUnitOfWork>();
        UnitOfWork = new CountingUnitOfWork(_seedUnitOfWork);
    }

    protected CountingUnitOfWork UnitOfWork { get; }

    protected T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    protected Task SeedAsync() => _seedUnitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

    protected async Task<MembershipPlan> SeedPlanAsync(int validityDays = 30, int? visitLimit = null, decimal price = 800m)
    {
        var plan = TestData.Plan(validityDays, visitLimit, price);
        Get<IMembershipPlanRepository>().Add(plan);
        await SeedAsync();
        return plan;
    }

    public ValueTask DisposeAsync() => _scope.DisposeAsync();
}
