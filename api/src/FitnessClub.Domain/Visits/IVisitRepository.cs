using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Visits;

public interface IVisitRepository : IRepository<Visit>
{
    Task<IReadOnlyList<Visit>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken);
}
