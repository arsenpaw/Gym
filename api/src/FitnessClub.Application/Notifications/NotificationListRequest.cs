using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Application.Notifications;

public sealed record NotificationListRequest
{
    [RegularExpression("^(?i:pending|sent|failed)$", ErrorMessage = "Status must be Pending, Sent or Failed.")]
    public string? Status { get; init; }
}
