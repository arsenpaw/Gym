using FitnessClub.Application.Abstractions;

namespace FitnessClub.IntegrationTests.Services;

public sealed class CountingUnitOfWork(IUnitOfWork inner) : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        await inner.SaveChangesAsync(cancellationToken);
    }
}
