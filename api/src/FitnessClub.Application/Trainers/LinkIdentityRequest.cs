using System.ComponentModel.DataAnnotations;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Application.Trainers;

public sealed record LinkIdentityRequest
{
    [Required]
    [StringLength(Trainer.IdentityUserIdMaxLength)]
    public string IdentityUserId { get; init; } = "";
}
