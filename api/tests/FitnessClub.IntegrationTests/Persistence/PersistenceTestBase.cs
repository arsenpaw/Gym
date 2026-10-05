using FitnessClub.Application.Abstractions;
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

    protected static string UniquePhone() => $"+380{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
}
