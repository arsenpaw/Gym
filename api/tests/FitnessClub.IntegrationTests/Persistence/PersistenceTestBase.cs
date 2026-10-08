using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Persistence;

public abstract class PersistenceTestBase(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    protected FitnessClubApiFactory Factory { get; } = factory;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    protected static readonly DateOnly Today = new(2026, 10, 5);

    protected async Task SaveAsync<TRepository>(Action<TRepository> change) where TRepository : notnull
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        change(scope.ServiceProvider.GetRequiredService<TRepository>());
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
    }

    protected async Task<TResult> ReadAsync<TRepository, TResult>(Func<TRepository, Task<TResult>> read) where TRepository : notnull
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<TRepository>());
    }

    protected Task ChangeAsync<TRepository>(Func<TRepository, Task> change) where TRepository : notnull =>
        InUnitOfWorkAsync(services => change(services.GetRequiredService<TRepository>()));

    protected async Task InUnitOfWorkAsync(Func<IServiceProvider, Task> work)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
    }

    protected async Task<MembershipPlan> SavedPlanAsync(int validityDays = 30, int? visitLimit = null)
    {
        var plan = MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(800m), validityDays, visitLimit);
        await SaveAsync<IMembershipPlanRepository>(plans => plans.Add(plan));
        return plan;
    }

    protected static string UniquePhone() => $"+380{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
}
