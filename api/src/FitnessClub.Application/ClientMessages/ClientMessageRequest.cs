using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Application.ClientMessages;

public sealed record ClientMessageRequest
{
    [Required(ErrorMessage = "Template is required.")]
    [RegularExpression("^(?i:expiryreminder|promotion)$", ErrorMessage = "Template must be ExpiryReminder or Promotion.")]
    public string? Template { get; init; }
}
