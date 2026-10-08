using FitnessClub.Application.Common;
using FitnessClub.Application.Notifications;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;
using FitnessClub.Domain.Payments;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;

namespace FitnessClub.IntegrationTests.Services;

public class ExpiryNotificationServiceTests(FitnessClubApiFactory factory) : ServiceTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeNotificationSender _sender = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);

    private ExpiryNotificationService Service(int expiryNoticeDays = 7) =>
        new(Get<IClientRepository>(), Get<INotificationRepository>(), _sender, UnitOfWork, _time,
            new ExpiryNotificationOptions { ExpiryNoticeDays = expiryNoticeDays });

    private Task<IReadOnlyList<Notification>> AllNotificationsAsync() => Get<INotificationRepository>().ListAsync(null, Ct);

    private async Task<Client> ClientWithMembershipEndingInAsync(int days, int validityDays = 30, string? email = "olena@example.com")
    {
        var client = TestData.Client(email);
        var startedDaysAgo = validityDays - 1 - days;
        client.PurchaseMembership(
            await SeedPlanAsync(validityDays), TestData.Today.AddDays(-startedDaysAgo), PaymentMethod.Cash, TestData.Now.AddDays(-startedDaysAgo));
        Get<IClientRepository>().Add(client);
        await SeedAsync();
        return client;
    }

    private async Task<Notification> PendingNoticeFor(string email)
    {
        await ClientWithMembershipEndingInAsync(3, email: email);
        await Service().CreateDueNoticesAsync(Ct);
        return (await AllNotificationsAsync()).Single(n => n.Recipient == email);
    }

    [Fact]
    public async Task CreateDueNoticesAsync_creates_a_pending_notice_for_a_membership_ending_within_the_window()
    {
        var client = await ClientWithMembershipEndingInAsync(4);
        var membership = client.Memberships.Single();

        var created = await Service().CreateDueNoticesAsync(Ct);

        Assert.Equal(1, created);
        var notice = Assert.Single(await AllNotificationsAsync());
        Assert.Equal(client.Id, notice.ClientId);
        Assert.Equal(membership.Id, notice.MembershipId);
        Assert.Equal(NotificationType.MembershipExpiring, notice.Type);
        Assert.Equal(NotificationChannel.Email, notice.Channel);
        Assert.Equal("olena@example.com", notice.Recipient);
        Assert.Equal(NotificationStatus.Pending, notice.Status);
        Assert.Equal(TestData.Now, notice.CreatedAt);
        Assert.Contains("2026-10-09", notice.Message);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateDueNoticesAsync_uses_sms_when_the_client_has_no_email()
    {
        var client = await ClientWithMembershipEndingInAsync(2, email: null);

        await Service().CreateDueNoticesAsync(Ct);

        var notice = Assert.Single(await AllNotificationsAsync());
        Assert.Equal(NotificationChannel.Sms, notice.Channel);
        Assert.Equal(client.Phone.Value, notice.Recipient);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task CreateDueNoticesAsync_includes_both_window_edges(int endsInDays)
    {
        await ClientWithMembershipEndingInAsync(endsInDays);

        Assert.Equal(1, await Service().CreateDueNoticesAsync(Ct));
    }

    [Fact]
    public async Task CreateDueNoticesAsync_ignores_memberships_ending_after_the_window()
    {
        await ClientWithMembershipEndingInAsync(8);

        Assert.Equal(0, await Service().CreateDueNoticesAsync(Ct));
        Assert.Empty(await AllNotificationsAsync());
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateDueNoticesAsync_uses_the_configured_window()
    {
        await ClientWithMembershipEndingInAsync(5);

        Assert.Equal(0, await Service(expiryNoticeDays: 3).CreateDueNoticesAsync(Ct));
        Assert.Equal(1, await Service(expiryNoticeDays: 5).CreateDueNoticesAsync(Ct));
    }

    [Fact]
    public async Task CreateDueNoticesAsync_run_twice_creates_no_duplicates()
    {
        await ClientWithMembershipEndingInAsync(4);

        await Service().CreateDueNoticesAsync(Ct);
        _time.Now = TestData.Now.AddDays(1);
        var secondRun = await Service().CreateDueNoticesAsync(Ct);

        Assert.Equal(0, secondRun);
        Assert.Single(await AllNotificationsAsync());
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateDueNoticesAsync_skips_a_membership_the_client_has_already_renewed()
    {
        var client = await ClientWithMembershipEndingInAsync(4);
        client.PurchaseMembership(await SeedPlanAsync(30), TestData.Today.AddDays(5), PaymentMethod.Cash, TestData.Now);

        Assert.Equal(0, await Service().CreateDueNoticesAsync(Ct));
        Assert.Empty(await AllNotificationsAsync());
    }

    [Fact]
    public async Task CreateDueNoticesAsync_skips_a_cancelled_membership()
    {
        var client = await ClientWithMembershipEndingInAsync(4);
        client.CancelMembership(client.Memberships.Single().Id, TestData.Now);

        Assert.Equal(0, await Service().CreateDueNoticesAsync(Ct));
        Assert.Empty(await AllNotificationsAsync());
    }

    [Fact]
    public async Task CreateDueNoticesAsync_skips_a_pass_that_does_not_outlast_the_window()
    {
        var singleVisit = TestData.Client("single@example.com");
        TestData.Buy(singleVisit, await SeedPlanAsync(validityDays: 1, visitLimit: 1));
        var notStarted = TestData.Client("future@example.com");
        TestData.Buy(notStarted, await SeedPlanAsync(validityDays: 5), TestData.Today.AddDays(1));
        Get<IClientRepository>().Add(singleVisit);
        Get<IClientRepository>().Add(notStarted);
        await SeedAsync();

        Assert.Equal(0, await Service().CreateDueNoticesAsync(Ct));
    }

    [Fact]
    public async Task SendPendingAsync_sends_and_marks_notices_sent()
    {
        var notice = await PendingNoticeFor("olena@example.com");
        _time.Now = TestData.Now.AddMinutes(5);

        var result = await Service().SendPendingAsync(Ct);

        Assert.Equal(new NotificationDeliveryResult(1, 0), result);
        Assert.Equal([(NotificationChannel.Email, "olena@example.com", notice.Message)], _sender.Sent);
        Assert.Equal(NotificationStatus.Sent, notice.Status);
        Assert.Equal(TestData.Now.AddMinutes(5), notice.SentAt);
    }

    [Fact]
    public async Task SendPendingAsync_marks_a_failed_send_and_continues_with_the_rest()
    {
        var failing = await PendingNoticeFor("broken@example.com");
        var working = await PendingNoticeFor("olena@example.com");
        _sender.FailingRecipients.Add("broken@example.com");

        var result = await Service().SendPendingAsync(Ct);

        Assert.Equal(new NotificationDeliveryResult(1, 1), result);
        Assert.Equal(NotificationStatus.Failed, failing.Status);
        Assert.Equal(FakeNotificationSender.FailureMessage, failing.FailureReason);
        Assert.Null(failing.SentAt);
        Assert.Equal(NotificationStatus.Sent, working.Status);
    }

    [Fact]
    public async Task SendPendingAsync_does_not_send_already_processed_notices_again()
    {
        await PendingNoticeFor("olena@example.com");
        await Service().SendPendingAsync(Ct);

        var secondRun = await Service().SendPendingAsync(Ct);

        Assert.Equal(new NotificationDeliveryResult(0, 0), secondRun);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task RetryAsync_puts_a_failed_notice_back_in_the_queue_for_the_next_send()
    {
        var notice = await PendingNoticeFor("broken@example.com");
        _sender.FailingRecipients.Add("broken@example.com");
        await Service().SendPendingAsync(Ct);
        _sender.FailingRecipients.Clear();

        await Service().RetryAsync(notice.Id, Ct);

        Assert.Equal(NotificationStatus.Pending, notice.Status);
        Assert.Null(notice.FailureReason);
        Assert.Equal(new NotificationDeliveryResult(1, 0), await Service().SendPendingAsync(Ct));
        Assert.Equal(NotificationStatus.Sent, notice.Status);
    }

    [Fact]
    public async Task RetryAsync_for_a_notice_that_did_not_fail_throws_domain_exception()
    {
        var notice = await PendingNoticeFor("olena@example.com");
        var saves = UnitOfWork.SaveCount;

        await Assert.ThrowsAsync<DomainException>(() => Service().RetryAsync(notice.Id, Ct));
        Assert.Equal(saves, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task RetryAsync_for_a_missing_notice_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().RetryAsync(Guid.NewGuid(), Ct));
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ListAsync_filters_by_status_ignoring_case()
    {
        var failing = await PendingNoticeFor("broken@example.com");
        var pending = await PendingNoticeFor("olena@example.com");
        failing.MarkFailed("Mailbox unavailable.");
        await SeedAsync();

        var all = await Service().ListAsync(new NotificationListRequest(), Ct);
        var failed = await Service().ListAsync(new NotificationListRequest { Status = "failed" }, Ct);

        Assert.Equal(2, all.Count);
        var only = Assert.Single(failed);
        Assert.Equal(failing.Id, only.Id);
        Assert.Equal("Failed", only.Status);
        Assert.Equal("Email", only.Channel);
        Assert.Equal("MembershipExpiring", only.Type);
        Assert.DoesNotContain(failed, n => n.Id == pending.Id);
    }

    [Fact]
    public async Task RunAsync_creates_due_notices_then_sends_them()
    {
        await ClientWithMembershipEndingInAsync(1);

        var result = await Service().RunAsync(Ct);

        Assert.Equal(new NotificationRunResponse(1, 1, 0), result);
        Assert.Equal(NotificationStatus.Sent, Assert.Single(await AllNotificationsAsync()).Status);
    }
}
