using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Rooms;

public interface IRoomRepository : IRepository<Room>
{
    Task<IReadOnlyList<Room>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);
}
