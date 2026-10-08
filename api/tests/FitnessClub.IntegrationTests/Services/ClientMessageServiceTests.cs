using FitnessClub.Application.Abstractions;
using FitnessClub.Application.ClientMessages;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;

namespace FitnessClub.IntegrationTests.Services;

public class ClientMessageServiceTests(FitnessClubApiFactory factory) : ServiceTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeNotificationSender _sender = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);

    private ClientMessageService Service() =>
        new(Get<IClientRepository>(), Get<INotificationRepository>(), _sender, Get<IEmailTemplates>(), UnitOfWork, _time);

    private static ClientMessageRequest Request(string template) => new() { Template = template };

    private Task<IReadOnlyList<Notification>> AllNotificationsAsync() => Get<INotificationRepository>().ListAsync(null, Ct);

    private async Task<Client> SeedClientAsync(string? email = "olena@example.com", bool withMembership = true)
    {
        var client = TestData.Client(email);
        if (withMembership)
            TestData.Buy(client, await SeedPlanAsync());
        Get<IClientRepository>().Add(client);
        await SeedAsync();
        return client;
    }

    [Fact]
    public async Task PreviewAsync_renders_the_reminder_for_the_active_membership_and_saves_nothing()
    {
        var client = await SeedClientAsync();
        var membership = client.Memberships.Single();

        var preview = await Service().PreviewAsync(client.Id, Request("ExpiryReminder"), Ct);

        Assert.Equal("ExpiryReminder", preview.Template);
        Assert.Equal("olena@example.com", preview.Recipient);
        Assert.Equal("Your membership expires soon", preview.Subject);
        Assert.Contains(membership.PlanName, preview.Html);
        Assert.Contains("3 November 2026", preview.Html);
        Assert.Equal(0, UnitOfWork.SaveCount);
        Assert.Empty(await AllNotificationsAsync());
    }

    [Fact]
    public async Task PreviewAsync_accepts_the_template_name_in_any_case()
    {
        var client = await SeedClientAsync();

        var preview = await Service().PreviewAsync(client.Id, Request("promotion"), Ct);

        Assert.Equal("Promotion", preview.Template);
    }

    [Fact]
    public async Task PreviewAsync_renders_the_promotion_valid_until_the_end_of_the_month()
    {
        var client = await SeedClientAsync(withMembership: false);

        var preview = await Service().PreviewAsync(client.Id, Request("Promotion"), Ct);

        Assert.Equal("10% off your next membership", preview.Subject);
        Assert.Contains("10% OFF", preview.Html);
        Assert.Contains("31 October 2026", preview.Html);
    }

    [Fact]
    public async Task PreviewAsync_reminder_without_an_active_membership_throws_domain_exception()
    {
        var client = await SeedClientAsync(withMembership: false);

        var exception = await Assert.ThrowsAsync<DomainException>(() => Service().PreviewAsync(client.Id, Request("ExpiryReminder"), Ct));
        Assert.Equal("The client has no active membership to remind about.", exception.Message);
    }

    [Fact]
    public async Task PreviewAsync_for_a_client_without_email_throws_domain_exception()
    {
        var client = await SeedClientAsync(email: null);

        var exception = await Assert.ThrowsAsync<DomainException>(() => Service().PreviewAsync(client.Id, Request("Promotion"), Ct));
        Assert.Equal("The client has no email address.", exception.Message);
    }

    [Fact]
    public async Task PreviewAsync_for_a_missing_client_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().PreviewAsync(Guid.NewGuid(), Request("Promotion"), Ct));
    }

    [Fact]
    public async Task SendAsync_stores_the_message_sends_it_and_marks_it_sent()
    {
        var client = await SeedClientAsync();
        _time.Now = TestData.Now.AddMinutes(5);

        var result = await Service().SendAsync(client.Id, Request("Promotion"), Ct);

        Assert.Equal("Sent", result.Status);
        Assert.Equal("Promotion", result.Type);
        Assert.Equal("10% off your next membership", result.Subject);
        Assert.Equal(TestData.Now.AddMinutes(5), result.SentAt);
        var sent = Assert.Single(_sender.Sent);
        Assert.Equal(result.Id, sent.Id);
        Assert.Contains("10% OFF", sent.HtmlBody);
        var stored = Assert.Single(await AllNotificationsAsync());
        Assert.Equal(NotificationStatus.Sent, stored.Status);
        Assert.Null(stored.MembershipId);
        Assert.Equal(2, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task SendAsync_records_the_failure_reason_when_sending_fails()
    {
        var client = await SeedClientAsync();
        _sender.FailingRecipients.Add("olena@example.com");

        var result = await Service().SendAsync(client.Id, Request("ExpiryReminder"), Ct);

        Assert.Equal("Failed", result.Status);
        Assert.Equal(FakeNotificationSender.FailureMessage, result.FailureReason);
        Assert.Null(result.SentAt);
        Assert.Equal(NotificationStatus.Failed, Assert.Single(await AllNotificationsAsync()).Status);
    }

    [Theory]
    [InlineData(2026, 12, 31, "31 December 2026")]
    [InlineData(2028, 2, 10, "29 February 2028")]
    [InlineData(2027, 2, 1, "28 February 2027")]
    public async Task SendAsync_promotion_is_valid_until_the_last_day_of_the_month(int year, int month, int day, string validUntil)
    {
        var client = await SeedClientAsync(withMembership: false);
        _time.Now = new DateTimeOffset(year, month, day, 20, 0, 0, TimeSpan.Zero);

        await Service().SendAsync(client.Id, Request("Promotion"), Ct);

        Assert.Contains(validUntil, Assert.Single(_sender.Sent).HtmlBody);
    }

    [Fact]
    public async Task SendAsync_for_a_client_without_email_saves_nothing()
    {
        var client = await SeedClientAsync(email: null);

        await Assert.ThrowsAsync<DomainException>(() => Service().SendAsync(client.Id, Request("Promotion"), Ct));
        Assert.Equal(0, UnitOfWork.SaveCount);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task ListAsync_returns_only_the_clients_messages_newest_first()
    {
        var olena = await SeedClientAsync();
        var other = await SeedClientAsync(email: "ivan@example.com", withMembership: false);
        var promotion = await Service().SendAsync(olena.Id, Request("Promotion"), Ct);
        _time.Now = TestData.Now.AddHours(1);
        var reminder = await Service().SendAsync(olena.Id, Request("ExpiryReminder"), Ct);
        await Service().SendAsync(other.Id, Request("Promotion"), Ct);

        var list = await Service().ListAsync(olena.Id, Ct);

        Assert.Equal([reminder.Id, promotion.Id], list.Select(n => n.Id));
    }

    [Fact]
    public async Task ListAsync_for_a_missing_client_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().ListAsync(Guid.NewGuid(), Ct));
    }
}
