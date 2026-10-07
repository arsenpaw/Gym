using FitnessClub.Domain.Rooms;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly List<Room> _rooms = [];

    public Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_rooms.FirstOrDefault(r => r.Id == id));

    public void Add(Room aggregate) => _rooms.Add(aggregate);

    public Task<IReadOnlyList<Room>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Room>>(_rooms.Where(r => includeInactive || r.IsActive).OrderBy(r => r.Name).ToList());

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(_rooms.Any(r => r.Id != excludeId && string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));
}
