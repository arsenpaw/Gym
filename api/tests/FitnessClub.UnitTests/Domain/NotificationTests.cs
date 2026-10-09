using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.UnitTests.Domain;

public class NotificationTests
{
    [Fact]
    public void MembershipExpiring_uses_email_and_the_given_content()
    {
        var client = TestData.Client(email: "olena@example.com");
        var membership = TestData.Buy(client, TestData.Plan(validityDays: 30));

        var notification = Notification.MembershipExpiring(client, membership, TestData.Content(), TestData.Now);

        Assert.Equal(NotificationType.MembershipExpiring, notification.Type);
        Assert.Equal(NotificationChannel.Email, notification.Channel);
        Assert.Equal("olena@example.com", notification.Recipient);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(membership.Id, notification.MembershipId);
        Assert.Equal("Your membership expires soon", notification.Subject);
        Assert.Equal("Dear Olena, your membership ends soon.", notification.Message);
        Assert.Equal("<p>Hello</p>", notification.HtmlBody);
        Assert.Equal(TestData.Now, notification.CreatedAt);
    }

    [Fact]
    public void MembershipExpiring_for_cancelled_membership_throws()
    {
        var client = TestData.ClientWithMembership();
        var membership = client.Memberships.Single();
        client.CancelMembership(membership.Id, TestData.Now);

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, membership, TestData.Content(), TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_after_renewal_throws()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);
        var current = client.Memberships.Single();
        TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(30));

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, current, TestData.Content(), TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_for_another_clients_membership_throws()
    {
        var owner = TestData.ClientWithMembership();

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(TestData.Client(), owner.Memberships.Single(), TestData.Content(), TestData.Now));
    }

    [Fact]
    public void ExpiryReminder_is_a_manual_email_without_membership_id()
    {
        var client = TestData.ClientWithMembership(email: "olena@example.com");

        var notification = Notification.ExpiryReminder(client, client.Memberships.Single(), TestData.Content(), TestData.Now);

        Assert.Equal(NotificationType.ExpiryReminder, notification.Type);
        Assert.Equal(NotificationChannel.Email, notification.Channel);
        Assert.Equal("olena@example.com", notification.Recipient);
        Assert.Null(notification.MembershipId);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
    }

    [Fact]
    public void ExpiryReminder_for_a_membership_not_active_today_throws()
    {
        var client = TestData.Client("olena@example.com");
        var future = TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(3));

        var exception = Assert.Throws<DomainException>(() => Notification.ExpiryReminder(client, future, TestData.Content(), TestData.Now));
        Assert.Equal("The client has no active membership to remind about.", exception.Message);
    }

    [Fact]
    public void ExpiryReminder_for_another_clients_membership_throws()
    {
        var owner = TestData.ClientWithMembership();

        Assert.Throws<DomainException>(() =>
            Notification.ExpiryReminder(TestData.Client("other@example.com"), owner.Memberships.Single(), TestData.Content(), TestData.Now));
    }

    [Fact]
    public void Promotion_is_a_manual_email_without_membership_id()
    {
        var client = TestData.Client("olena@example.com");

        var notification = Notification.Promotion(client, TestData.Content(), TestData.Now);

        Assert.Equal(NotificationType.Promotion, notification.Type);
        Assert.Equal("olena@example.com", notification.Recipient);
        Assert.Null(notification.MembershipId);
    }

    [Fact]
    public void Content_without_html_leaves_html_body_empty()
    {
        var notification = Notification.Promotion(TestData.Client("olena@example.com"), TestData.Content(html: null), TestData.Now);

        Assert.Null(notification.HtmlBody);
    }

    [Fact]
    public void MarkSent_records_time_and_cannot_be_repeated()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Content(), TestData.Now);

        notification.MarkSent(TestData.Now.AddMinutes(1));

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(TestData.Now.AddMinutes(1), notification.SentAt);
        Assert.Throws<DomainException>(() => notification.MarkFailed("late failure"));
    }

    [Fact]
    public void MarkFailed_truncates_long_reason()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Content(), TestData.Now);

        notification.MarkFailed(new string('x', Notification.FailureReasonMaxLength + 50));

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal(Notification.FailureReasonMaxLength, notification.FailureReason!.Length);
    }

    [Fact]
    public void Retry_returns_failed_notification_to_pending()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Content(), TestData.Now);
        Assert.Throws<DomainException>(() => notification.Retry());
        notification.MarkFailed("Mailbox unavailable.");

        notification.Retry();

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Null(notification.FailureReason);
    }

    [Fact]
    public void Retry_with_new_content_replaces_subject_text_and_html()
    {
        var notification = Notification.Promotion(TestData.Client("olena@example.com"), TestData.Content(), TestData.Now);
        notification.MarkFailed("Mailbox unavailable.");

        notification.Retry(NotificationContent.Create("New subject", "New text", "<p>New</p>"));

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal("New subject", notification.Subject);
        Assert.Equal("New text", notification.Message);
        Assert.Equal("<p>New</p>", notification.HtmlBody);
    }
}
