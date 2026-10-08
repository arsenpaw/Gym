using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.IntegrationTests.Services;

internal sealed class FakeNotificationSender : INotificationSender
{
    public const string FailureMessage = "Mailbox unavailable.";

    private readonly List<Notification> _sent = [];

    public IReadOnlyList<Notification> Sent => _sent;

    public HashSet<string> FailingRecipients { get; } = [];

    public Action? OnSend { get; set; }

    public Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (FailingRecipients.Contains(notification.Recipient))
            throw new InvalidOperationException(FailureMessage);

        OnSend?.Invoke();
        _sent.Add(notification);
        return Task.CompletedTask;
    }
}
