using FitnessClub.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository(FitnessClubDbContext db) : Repository<Room>(db), IRoomRepository
{
    public async Task<IReadOnlyList<Room>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await Set.Where(r => includeInactive || r.IsActive).OrderBy(r => r.Name).ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLower();
        return Set.AnyAsync(r => r.Id != excludeId && r.Name.ToLower() == normalizedName, cancellationToken);
    }
}
