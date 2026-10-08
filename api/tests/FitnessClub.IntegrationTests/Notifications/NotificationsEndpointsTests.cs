using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;
using FitnessClub.Application.Notifications;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Notifications;

public class NotificationsEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private const string BaseUrl = "/api/notifications";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient Admin() => factory.CreateClientWithRoles(Roles.Admin);

    [Fact]
    public async Task List_as_admin_returns_notifications()
    {
        var seeded = await factory.NotificationAsync();

        var list = await Admin().GetFromJsonAsync<List<NotificationResponse>>(BaseUrl, Ct);

        var notification = Assert.Single(list!, n => n.Id == seeded.Id);
        Assert.Equal(seeded.ClientId, notification.ClientId);
        Assert.Equal(seeded.MembershipId, notification.MembershipId);
        Assert.Equal("MembershipExpiring", notification.Type);
        Assert.Equal("Email", notification.Channel);
        Assert.Equal(seeded.Recipient, notification.Recipient);
        Assert.Equal(seeded.Subject, notification.Subject);
        Assert.Equal(seeded.Message, notification.Message);
        Assert.Equal("Pending", notification.Status);
        Assert.Equal(seeded.CreatedAt, notification.CreatedAt);
        Assert.Null(notification.SentAt);
        Assert.Null(notification.FailureReason);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var failed = await factory.NotificationAsync("Mailbox unavailable.");
        var pending = await factory.NotificationAsync();

        var list = await Admin().GetFromJsonAsync<List<NotificationResponse>>($"{BaseUrl}?status=failed", Ct);

        Assert.Contains(list!, n => n.Id == failed.Id && n.FailureReason == "Mailbox unavailable.");
        Assert.DoesNotContain(list!, n => n.Id == pending.Id);
        Assert.All(list!, n => Assert.Equal("Failed", n.Status));
    }

    [Fact]
    public async Task List_with_unknown_status_returns_400_problem_details()
    {
        var response = await Admin().GetAsync($"{BaseUrl}?status=Delivered", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_without_user_returns_401()
    {
        var response = await factory.CreateClient().GetAsync(BaseUrl, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Receptionist)]
    [InlineData(Roles.Trainer)]
    public async Task Endpoints_for_non_admin_return_403(string role)
    {
        var client = factory.CreateClientWithRoles(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(BaseUrl, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/retry", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{BaseUrl}/run", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Retry_failed_notification_returns_204_and_puts_it_back_to_pending()
    {
        var failed = await factory.NotificationAsync("Mailbox unavailable.");

        var response = await Admin().PostAsync($"{BaseUrl}/{failed.Id}/retry", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var list = await Admin().GetFromJsonAsync<List<NotificationResponse>>(BaseUrl, Ct);
        var retried = Assert.Single(list!, n => n.Id == failed.Id);
        Assert.Equal("Pending", retried.Status);
        Assert.Null(retried.FailureReason);
    }

    [Fact]
    public async Task Retry_notification_that_did_not_fail_returns_400()
    {
        var pending = await factory.NotificationAsync();

        var response = await Admin().PostAsync($"{BaseUrl}/{pending.Id}/retry", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Retry_missing_notification_returns_404()
    {
        var response = await Admin().PostAsync($"{BaseUrl}/{Guid.NewGuid()}/retry", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Run_creates_and_sends_due_notices_once()
    {
        var membership = await factory.ClientWithMembershipEndingInAsync(3);

        var response = await Admin().PostAsync($"{BaseUrl}/run", null, Ct);
        var secondRun = await Admin().PostAsync($"{BaseUrl}/run", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<NotificationRunResponse>(Ct);
        Assert.True(result!.Created >= 1);
        Assert.True(result.Sent >= 1);
        Assert.Equal(0, result.Failed);
        secondRun.EnsureSuccessStatusCode();
        var sent = await Admin().GetFromJsonAsync<List<NotificationResponse>>($"{BaseUrl}?status=Sent", Ct);
        var notice = Assert.Single(sent!, n => n.MembershipId == membership.Id);
        Assert.NotNull(notice.SentAt);
    }
}
