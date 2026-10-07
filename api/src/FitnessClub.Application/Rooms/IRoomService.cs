namespace FitnessClub.Application.Rooms;

public interface IRoomService
{
    Task<IReadOnlyList<RoomResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<RoomResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<RoomResponse> CreateAsync(RoomRequest request, CancellationToken cancellationToken);

    Task<RoomResponse> UpdateAsync(Guid id, RoomRequest request, CancellationToken cancellationToken);

    Task ActivateAsync(Guid id, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
