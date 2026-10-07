using System.ComponentModel.DataAnnotations;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Application.Trainers;

public sealed record TrainerRequest
{
    public const int PhoneInputMaxLength = 32;

    [Required]
    [StringLength(PersonName.PartMaxLength)]
    public string FirstName { get; init; } = "";

    [Required]
    [StringLength(PersonName.PartMaxLength)]
    public string LastName { get; init; } = "";

    [StringLength(PersonName.PartMaxLength)]
    public string? MiddleName { get; init; }

    [Required]
    [StringLength(PhoneInputMaxLength)]
    public string Phone { get; init; } = "";

    [StringLength(EmailAddress.MaxLength)]
    public string? Email { get; init; }

    [Required]
    [StringLength(Trainer.SpecializationMaxLength)]
    public string Specialization { get; init; } = "";
}
