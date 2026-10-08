using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Notifications;

public sealed class Notification : AggregateRoot
{
    public const int RecipientMaxLength = 254;
    public const int MessageMaxLength = 1000;
    public const int FailureReasonMaxLength = 500;

    public Guid ClientId { get; private set; }
    public Guid? MembershipId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public string? HtmlBody { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Notification()
    {
    }

    public static Notification MembershipExpiring(Client client, Membership membership, NotificationContent content, DateTimeOffset now)
    {
        if (!client.Owns(membership))
            throw new DomainException("The membership does not belong to the client.");

        if (!client.NeedsExpiryNotice(membership, now.ToDateOnly()))
            throw new DomainException("The membership does not need an expiry notice.");

        return Email(client, membership.Id, NotificationType.MembershipExpiring, content, now);
    }

    public static Notification ExpiryReminder(Client client, Membership membership, NotificationContent content, DateTimeOffset now)
    {
        if (!client.Owns(membership))
            throw new DomainException("The membership does not belong to the client.");

        if (!membership.IsActiveOn(now.ToDateOnly()))
            throw new DomainException("The client has no active membership to remind about.");

        return Email(client, null, NotificationType.ExpiryReminder, content, now);
    }

    public static Notification Promotion(Client client, NotificationContent content, DateTimeOffset now) =>
        Email(client, null, NotificationType.Promotion, content, now);

    public void MarkSent(DateTimeOffset now)
    {
        EnsurePending();
        Status = NotificationStatus.Sent;
        SentAt = now;
    }

    public void MarkFailed(string reason)
    {
        EnsurePending();

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A failure reason is required.");

        var trimmed = reason.Trim();
        Status = NotificationStatus.Failed;
        FailureReason = trimmed.Length > FailureReasonMaxLength ? trimmed[..FailureReasonMaxLength] : trimmed;
    }

    public void Retry()
    {
        if (Status != NotificationStatus.Failed)
            throw new DomainException("Only a failed notification can be retried.");

        Status = NotificationStatus.Pending;
        FailureReason = null;
    }

    private static Notification Email(Client client, Guid? membershipId, NotificationType type, NotificationContent content, DateTimeOffset now)
    {
        var email = client.Email ?? throw new DomainException("The client has no email address.");
        return new Notification
        {
            ClientId = client.Id,
            MembershipId = membershipId,
            Type = type,
            Channel = NotificationChannel.Email,
            Recipient = email.Value,
            Subject = content.Subject,
            Message = content.Text,
            HtmlBody = content.Html,
            Status = NotificationStatus.Pending,
            CreatedAt = now,
        };
    }

    private void EnsurePending()
    {
        if (Status != NotificationStatus.Pending)
            throw new DomainException("The notification has already been processed.");
    }
}
