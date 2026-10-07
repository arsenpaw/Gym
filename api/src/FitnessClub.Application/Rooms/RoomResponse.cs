using FitnessClub.Domain.Rooms;

namespace FitnessClub.Application.Rooms;

public sealed record RoomResponse(Guid Id, string Name, int Capacity, bool IsActive)
{
    public static RoomResponse FromEntity(Room room) => new(room.Id, room.Name, room.Capacity, room.IsActive);
}
