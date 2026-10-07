using System.ComponentModel.DataAnnotations;
using FitnessClub.Domain.Rooms;

namespace FitnessClub.Application.Rooms;

public sealed record RoomRequest
{
    [Required]
    [StringLength(Room.NameMaxLength)]
    public string Name { get; init; } = "";

    [Range(1, Room.MaxCapacity)]
    public int Capacity { get; init; }
}
