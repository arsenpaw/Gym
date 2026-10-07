using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryTrainerRepository : ITrainerRepository
{
    private readonly List<Trainer> _trainers = [];

    public Task<Trainer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_trainers.FirstOrDefault(t => t.Id == id));

    public void Add(Trainer aggregate) => _trainers.Add(aggregate);

    public Task<IReadOnlyList<Trainer>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Trainer>>(
            _trainers.Where(t => includeInactive || t.IsActive).OrderBy(t => t.Name.LastName).ThenBy(t => t.Name.FirstName).ToList());

    public Task<bool> PhoneExistsAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(_trainers.Any(t => t.Id != excludeId && t.Phone == phone));

    public Task<Trainer?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken) =>
        Task.FromResult(_trainers.FirstOrDefault(t => t.IdentityUserId == identityUserId));
}
