using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FitnessClub.Application.Trainers;

public sealed record WorkingHoursRequest
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))]
    public DayOfWeek? Day { get; init; }

    [Required]
    public TimeOnly? Start { get; init; }

    [Required]
    public TimeOnly? End { get; init; }
}
