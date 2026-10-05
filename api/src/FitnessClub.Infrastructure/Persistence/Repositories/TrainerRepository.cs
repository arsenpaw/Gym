using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class TrainerRepository(FitnessClubDbContext db) : Repository<Trainer>(db), ITrainerRepository
{
    public async Task<IReadOnlyList<Trainer>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await Set
            .Where(t => includeInactive || t.IsActive)
            .OrderBy(t => t.Name.LastName)
            .ThenBy(t => t.Name.FirstName)
            .ToListAsync(cancellationToken);

    public Task<bool> PhoneExistsAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(t => t.Id != excludeId && t.Phone == phone, cancellationToken);

    public Task<Trainer?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(t => t.IdentityUserId == identityUserId, cancellationToken);
}
