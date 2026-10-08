using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace FitnessClub.Infrastructure.Notifications;

internal sealed class SmtpNotificationSender(IOptions<SmtpOptions> options) : INotificationSender
{
    public const string Subject = "Your membership expires soon";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task SendAsync(NotificationChannel channel, string recipient, string message, CancellationToken cancellationToken)
    {
        if (channel != NotificationChannel.Email)
            throw new InvalidOperationException($"{channel} notifications can't be sent: only email is supported.");

        var smtp = options.Value;
        var email = new MimeMessage
        {
            Subject = Subject,
            Body = new TextPart(TextFormat.Plain) { Text = message },
        };
        email.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress!));
        email.To.Add(MailboxAddress.Parse(recipient));

        using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
        await client.ConnectAsync(smtp.Host!, smtp.Port, SecureSocketOptions.Auto, cancellationToken);
        if (!string.IsNullOrEmpty(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, cancellationToken);
        await client.SendAsync(email, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
