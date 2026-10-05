using FitnessClub.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal abstract class Repository<TAggregate>(FitnessClubDbContext db) : IRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    protected DbSet<TAggregate> Set { get; } = db.Set<TAggregate>();

    public Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(aggregate => aggregate.Id == id, cancellationToken);

    public void Add(TAggregate aggregate) => Set.Add(aggregate);
}
