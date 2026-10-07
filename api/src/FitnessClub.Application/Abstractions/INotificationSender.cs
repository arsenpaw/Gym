using FitnessClub.Domain.Notifications;

namespace FitnessClub.Application.Abstractions;

public interface INotificationSender
{
    Task SendAsync(NotificationChannel channel, string recipient, string message, CancellationToken cancellationToken);
}
