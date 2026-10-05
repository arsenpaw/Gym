using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Trainers;

public interface ITrainerRepository : IRepository<Trainer>
{
    Task<IReadOnlyList<Trainer>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<bool> PhoneExistsAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken);

    Task<Trainer?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken);
}
