using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Visits;

public interface IVisitRepository : IRepository<Visit>
{
    Task<IReadOnlyList<Visit>> ListForClientAsync(Guid clientId, int skip, int take, CancellationToken cancellationToken);

    Task<int> CountForClientAsync(Guid clientId, CancellationToken cancellationToken);
}
