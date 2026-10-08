using FitnessClub.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(FitnessClubDbContext db) : Repository<Notification>(db), INotificationRepository
{
    public Task<bool> ExistsForMembershipAsync(Guid membershipId, NotificationType type, CancellationToken cancellationToken) =>
        Set.AnyAsync(n => n.MembershipId == membershipId && n.Type == type, cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListPendingAsync(CancellationToken cancellationToken) =>
        await Set.Where(n => n.Status == NotificationStatus.Pending).OrderBy(n => n.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await Set.Where(n => n.ClientId == clientId).OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListAsync(NotificationStatus? status, CancellationToken cancellationToken) =>
        await Set.Where(n => status == null || n.Status == status).OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
}
