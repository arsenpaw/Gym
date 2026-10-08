using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Notifications;

public sealed record NotificationContent
{
    public const int SubjectMaxLength = 200;

    public string Subject { get; }
    public string Text { get; }
    public string? Html { get; }

    private NotificationContent(string subject, string text, string? html)
    {
        Subject = subject;
        Text = text;
        Html = html;
    }

    public static NotificationContent Create(string subject, string text, string? html = null)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainException("A subject is required.");

        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("A message is required.");

        var trimmedSubject = subject.Trim();
        if (trimmedSubject.Length > SubjectMaxLength)
            throw new DomainException($"A subject must be at most {SubjectMaxLength} characters.");

        var trimmedText = text.Trim();
        if (trimmedText.Length > Notification.MessageMaxLength)
            throw new DomainException($"A message must be at most {Notification.MessageMaxLength} characters.");

        return new NotificationContent(trimmedSubject, trimmedText, string.IsNullOrWhiteSpace(html) ? null : html);
    }
}
