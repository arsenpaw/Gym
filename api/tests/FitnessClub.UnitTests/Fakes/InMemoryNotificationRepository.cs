using FitnessClub.Domain.Notifications;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _notifications = [];

    public IReadOnlyList<Notification> All => _notifications;

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_notifications.FirstOrDefault(n => n.Id == id));

    public void Add(Notification aggregate) => _notifications.Add(aggregate);

    public Task<bool> ExistsForMembershipAsync(Guid membershipId, NotificationType type, CancellationToken cancellationToken) =>
        Task.FromResult(_notifications.Any(n => n.MembershipId == membershipId && n.Type == type));

    public Task<IReadOnlyList<Notification>> ListPendingAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Notification>>(
            _notifications.Where(n => n.Status == NotificationStatus.Pending).OrderBy(n => n.CreatedAt).ToList());

    public Task<IReadOnlyList<Notification>> ListAsync(NotificationStatus? status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Notification>>(
            _notifications.Where(n => status == null || n.Status == status).OrderByDescending(n => n.CreatedAt).ToList());
}
