using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Training;

namespace FitnessClub.Application.Training;

public sealed record ScheduleSessionRequest
{
    [Required]
    [StringLength(TrainingSession.TitleMaxLength)]
    public string Title { get; init; } = "";

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<SessionType>))]
    public SessionType? Type { get; init; }

    [Required]
    public Guid? TrainerId { get; init; }

    [Required]
    public Guid? RoomId { get; init; }

    [Required]
    public DateTimeOffset? Start { get; init; }

    [Required]
    public DateTimeOffset? End { get; init; }

    [Range(1, Room.MaxCapacity)]
    public int Capacity { get; init; }
}
