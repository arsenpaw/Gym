namespace FitnessClub.Domain.Common;

public interface IRepository<TAggregate> where TAggregate : AggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(TAggregate aggregate);
}
