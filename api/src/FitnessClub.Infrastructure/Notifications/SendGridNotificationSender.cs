using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Notifications;
using Microsoft.Extensions.Options;

namespace FitnessClub.Infrastructure.Notifications;

internal sealed class SendGridNotificationSender(HttpClient http, IOptions<SendGridOptions> options) : INotificationSender
{
    public static readonly Uri BaseAddress = new("https://api.sendgrid.com/");

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (notification.Channel != NotificationChannel.Email)
            throw new InvalidOperationException($"{notification.Channel} notifications can't be sent: only email is supported.");

        var sendGrid = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/mail/send") { Content = JsonContent.Create(Mail(notification, sendGrid)) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sendGrid.ApiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var reason = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SendGrid rejected the email ({(int)response.StatusCode} {response.StatusCode}): {reason}");
        }
    }

    private static SendGridMail Mail(Notification notification, SendGridOptions sendGrid) =>
        new(
            [new SendGridPersonalization([new SendGridAddress(notification.Recipient, null)])],
            new SendGridAddress(sendGrid.FromAddress!, sendGrid.FromName),
            notification.Subject,
            notification.HtmlBody is null
                ? [new SendGridContent("text/plain", notification.Message)]
                : [new SendGridContent("text/plain", notification.Message), new SendGridContent("text/html", notification.HtmlBody)]);

    private sealed record SendGridMail(
        [property: JsonPropertyName("personalizations")] IReadOnlyList<SendGridPersonalization> Personalizations,
        [property: JsonPropertyName("from")] SendGridAddress From,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("content")] IReadOnlyList<SendGridContent> Content);

    private sealed record SendGridPersonalization([property: JsonPropertyName("to")] IReadOnlyList<SendGridAddress> To);

    private sealed record SendGridAddress(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name);

    private sealed record SendGridContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] string Value);
}
