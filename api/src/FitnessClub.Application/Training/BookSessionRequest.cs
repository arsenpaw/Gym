using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Application.Training;

public sealed record BookSessionRequest
{
    [Required]
    public Guid? ClientId { get; init; }
}
