using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Application.Notifications;

public sealed class ExpiryNotificationOptions
{
    public const string SectionName = "Notifications";
    public const int MaxExpiryNoticeDays = 60;

    [Range(1, MaxExpiryNoticeDays)]
    public int ExpiryNoticeDays { get; set; } = 7;
}
