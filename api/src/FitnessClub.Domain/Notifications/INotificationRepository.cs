using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Notifications;

public interface INotificationRepository : IRepository<Notification>
{
    Task<bool> ExistsForMembershipAsync(Guid membershipId, NotificationType type, CancellationToken cancellationToken);

    Task<IReadOnlyList<Notification>> ListPendingAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Notification>> ListAsync(NotificationStatus? status, CancellationToken cancellationToken);
}
