using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Rooms;

namespace FitnessClub.Application.Rooms;

internal sealed class RoomService(IRoomRepository rooms, IUnitOfWork unitOfWork) : IRoomService
{
    public async Task<IReadOnlyList<RoomResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var list = await rooms.ListAsync(includeInactive, cancellationToken);
        return list.Select(RoomResponse.FromEntity).ToList();
    }

    public async Task<RoomResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        RoomResponse.FromEntity(await FindAsync(id, cancellationToken));

    public async Task<RoomResponse> CreateAsync(RoomRequest request, CancellationToken cancellationToken)
    {
        var room = Room.Create(request.Name, request.Capacity);
        await EnsureNameIsUniqueAsync(request.Name, excludeId: null, cancellationToken);

        rooms.Add(room);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RoomResponse.FromEntity(room);
    }

    public async Task<RoomResponse> UpdateAsync(Guid id, RoomRequest request, CancellationToken cancellationToken)
    {
        var room = await FindAsync(id, cancellationToken);
        await EnsureNameIsUniqueAsync(request.Name, room.Id, cancellationToken);
        room.Update(request.Name, request.Capacity);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RoomResponse.FromEntity(room);
    }

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var room = await FindAsync(id, cancellationToken);
        room.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var room = await FindAsync(id, cancellationToken);
        room.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Room> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await rooms.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Room '{id}' was not found.");

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await rooms.NameExistsAsync(name, excludeId, cancellationToken))
            throw new ConflictException($"A room named '{name.Trim()}' already exists.");
    }
}
