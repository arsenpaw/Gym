using FitnessClub.Domain.Visits;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryVisitRepository : IVisitRepository
{
    private readonly List<Visit> _visits = [];

    public IReadOnlyList<Visit> All => _visits;

    public Task<Visit?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_visits.FirstOrDefault(v => v.Id == id));

    public void Add(Visit aggregate) => _visits.Add(aggregate);

    public Task<IReadOnlyList<Visit>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Visit>>(
            _visits.Where(v => v.ClientId == clientId).OrderByDescending(v => v.CheckedInAt).ToList());
}
