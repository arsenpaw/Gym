using FitnessClub.Domain.Notifications;

namespace FitnessClub.Application.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    Guid ClientId,
    Guid? MembershipId,
    string Type,
    string Channel,
    string Recipient,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    string? FailureReason)
{
    public static NotificationResponse FromEntity(Notification notification) =>
        new(
            notification.Id,
            notification.ClientId,
            notification.MembershipId,
            notification.Type.ToString(),
            notification.Channel.ToString(),
            notification.Recipient,
            notification.Message,
            notification.Status.ToString(),
            notification.CreatedAt,
            notification.SentAt,
            notification.FailureReason);
}
