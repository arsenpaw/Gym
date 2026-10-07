using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class FakeNotificationSender : INotificationSender
{
    public const string FailureMessage = "Mailbox unavailable.";

    private readonly List<(NotificationChannel Channel, string Recipient, string Message)> _sent = [];

    public IReadOnlyList<(NotificationChannel Channel, string Recipient, string Message)> Sent => _sent;

    public HashSet<string> FailingRecipients { get; } = [];

    public Task SendAsync(NotificationChannel channel, string recipient, string message, CancellationToken cancellationToken)
    {
        if (FailingRecipients.Contains(recipient))
            throw new InvalidOperationException(FailureMessage);

        _sent.Add((channel, recipient, message));
        return Task.CompletedTask;
    }
}
