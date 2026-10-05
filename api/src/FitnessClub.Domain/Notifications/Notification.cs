using System.Globalization;
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
    public string Message { get; private set; } = null!;
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Notification()
    {
    }

    public static Notification MembershipExpiring(Client client, Membership membership, DateTimeOffset now)
    {
        if (!client.Owns(membership))
            throw new DomainException("The membership does not belong to the client.");

        if (!client.NeedsExpiryNotice(membership, now.ToDateOnly()))
            throw new DomainException("The membership does not need an expiry notice.");

        var endsOn = membership.EndsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new Notification
        {
            ClientId = client.Id,
            MembershipId = membership.Id,
            Type = NotificationType.MembershipExpiring,
            Channel = client.Email is null ? NotificationChannel.Sms : NotificationChannel.Email,
            Recipient = client.Email?.Value ?? client.Phone.Value,
            Message = $"Dear {client.Name.FirstName}, your membership '{membership.PlanName}' expires on {endsOn}.",
            Status = NotificationStatus.Pending,
            CreatedAt = now,
        };
    }

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

    private void EnsurePending()
    {
        if (Status != NotificationStatus.Pending)
            throw new DomainException("The notification has already been processed.");
    }
}
