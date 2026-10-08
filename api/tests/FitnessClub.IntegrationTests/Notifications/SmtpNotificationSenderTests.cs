using System.Net.Http.Json;
using FitnessClub.Domain.Notifications;
using FitnessClub.Infrastructure.Notifications;
using Microsoft.Extensions.Options;

namespace FitnessClub.IntegrationTests.Notifications;

public class SmtpNotificationSenderTests(MailpitFixture mailpit) : IClassFixture<MailpitFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SmtpNotificationSender Sender(string? host = null, int? port = null) =>
        new(Options.Create(new SmtpOptions
        {
            Host = host ?? mailpit.Host,
            Port = port ?? mailpit.Smtp,
            FromAddress = "club@example.com",
            FromName = "Fitness Club",
        }));

    [Fact]
    public async Task SendAsync_delivers_a_plain_text_email()
    {
        var recipient = $"{Guid.NewGuid():N}@example.com";

        await Sender().SendAsync(NotificationChannel.Email, recipient, "Your membership expires on 2026-10-09.", Ct);

        using var api = mailpit.Api();
        var list = await api.GetFromJsonAsync<MessageList>($"/api/v1/search?query=to:{recipient}", Ct);
        var summary = Assert.Single(list!.Messages);
        Assert.Equal("club@example.com", summary.From.Address);
        Assert.Equal("Fitness Club", summary.From.Name);
        Assert.Equal(recipient, Assert.Single(summary.To).Address);
        Assert.Equal(SmtpNotificationSender.Subject, summary.Subject);
        var message = await api.GetFromJsonAsync<Message>($"/api/v1/message/{summary.Id}", Ct);
        Assert.Equal("Your membership expires on 2026-10-09.", message!.Text.Trim());
    }

    [Fact]
    public async Task SendAsync_rejects_a_channel_other_than_email()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Sender().SendAsync(NotificationChannel.Sms, "+380501234567", "Hello", Ct));

        Assert.Contains("email", exception.Message);
    }

    [Fact]
    public async Task SendAsync_to_an_unreachable_server_throws()
    {
        var exception = await Record.ExceptionAsync(
            () => Sender(host: "127.0.0.1", port: 1).SendAsync(NotificationChannel.Email, "olena@example.com", "Hello", Ct));

        Assert.NotNull(exception);
        Assert.IsNotType<InvalidOperationException>(exception);
    }

    private sealed record MessageList(IReadOnlyList<MessageSummary> Messages);

    private sealed record MessageSummary(string Id, Mailbox From, IReadOnlyList<Mailbox> To, string Subject);

    private sealed record Mailbox(string Name, string Address);

    private sealed record Message(string Text);
}
