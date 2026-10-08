using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.UnitTests.Domain;

public class NotificationTests
{
    [Fact]
    public void MembershipExpiring_uses_email_when_client_has_one()
    {
        var client = TestData.Client(email: "olena@example.com");
        var membership = TestData.Buy(client, TestData.Plan(validityDays: 30));

        var notification = Notification.MembershipExpiring(client, membership, TestData.Now);

        Assert.Equal(NotificationChannel.Email, notification.Channel);
        Assert.Equal("olena@example.com", notification.Recipient);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(membership.Id, notification.MembershipId);
        Assert.Contains("2026-11-03", notification.Message);
    }

    [Fact]
    public void MembershipExpiring_for_client_without_email_throws()
    {
        var client = TestData.ClientWithMembership(email: null);

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_for_cancelled_membership_throws()
    {
        var client = TestData.ClientWithMembership();
        var membership = client.Memberships.Single();
        client.CancelMembership(membership.Id, TestData.Now);

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, membership, TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_after_renewal_throws()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);
        var current = client.Memberships.Single();
        TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(30));

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, current, TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_for_another_clients_membership_throws()
    {
        var owner = TestData.ClientWithMembership();

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(TestData.Client(), owner.Memberships.Single(), TestData.Now));
    }

    [Fact]
    public void MarkSent_records_time_and_cannot_be_repeated()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Now);

        notification.MarkSent(TestData.Now.AddMinutes(1));

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(TestData.Now.AddMinutes(1), notification.SentAt);
        Assert.Throws<DomainException>(() => notification.MarkFailed("late failure"));
    }

    [Fact]
    public void MarkFailed_truncates_long_reason()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Now);

        notification.MarkFailed(new string('x', Notification.FailureReasonMaxLength + 50));

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal(Notification.FailureReasonMaxLength, notification.FailureReason!.Length);
    }

    [Fact]
    public void Retry_returns_failed_notification_to_pending()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Now);
        Assert.Throws<DomainException>(() => notification.Retry());
        notification.MarkFailed("Mailbox unavailable.");

        notification.Retry();

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Null(notification.FailureReason);
    }
}
