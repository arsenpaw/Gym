using System.ComponentModel.DataAnnotations;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Application.Clients;

public sealed record ClientRequest
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
    public DateOnly? DateOfBirth { get; init; }

    [Required]
    [StringLength(EmailAddress.MaxLength)]
    public string Email { get; init; } = "";

    [StringLength(PhoneInputMaxLength)]
    public string? Phone { get; init; }
}
