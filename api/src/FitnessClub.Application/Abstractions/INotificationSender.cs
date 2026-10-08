using FitnessClub.Domain.Notifications;

namespace FitnessClub.Application.Abstractions;

public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
