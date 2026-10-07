using FitnessClub.Domain.Clients;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryClientRepository : IClientRepository
{
    private readonly List<Client> _clients = [];

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_clients.FirstOrDefault(c => c.Id == id));

    public void Add(Client aggregate) => _clients.Add(aggregate);

    public Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Client>>(
            _clients.OrderBy(c => c.Name.LastName).ThenBy(c => c.Name.FirstName).ToList());

    public Task<bool> PhoneExistsAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(_clients.Any(c => c.Id != excludeId && c.Phone == phone));

    public Task<IReadOnlyList<Client>> ListWithMembershipsEndingBetweenAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Client>>(
            _clients.Where(c => c.Memberships.Any(m => !m.IsCancelled && m.EndsOn >= from && m.EndsOn <= to)).ToList());
}
