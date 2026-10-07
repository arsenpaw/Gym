using FitnessClub.Application.Reports;
using FitnessClub.Domain.Training;
using FitnessClub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Reports;

internal sealed class ReportQueries(FitnessClubDbContext db) : IReportQueries
{
    public async Task<IReadOnlyList<ClientActivityItem>> ListClientActivityAsync(
        DateTimeOffset visitsFrom, DateTimeOffset visitsTo, DateOnly today, CancellationToken cancellationToken)
    {
        var clients = await db.Clients.AsNoTracking().ToListAsync(cancellationToken);

        var visits = await db.Visits.AsNoTracking()
            .GroupBy(visit => visit.ClientId)
            .Select(group => new
            {
                ClientId = group.Key,
                InRange = group.Count(visit => visit.CheckedInAt >= visitsFrom && visit.CheckedInAt < visitsTo),
                LastVisitAt = group.Max(visit => visit.CheckedInAt),
            })
            .ToDictionaryAsync(stats => stats.ClientId, cancellationToken);

        return clients
            .OrderBy(client => client.Name.FullName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(client => client.Id)
            .Select(client =>
            {
                var active = client.ActiveMembershipOn(today);
                var stats = visits.GetValueOrDefault(client.Id);
                return new ClientActivityItem(
                    client.Id,
                    client.Name.FullName,
                    client.AgeOn(today),
                    client.Phone.Value,
                    active is null
                        ? null
                        : new ActiveMembershipSummary(active.Id, active.PlanName, active.StartsOn, active.EndsOn, active.RemainingVisits),
                    stats?.InRange ?? 0,
                    stats?.LastVisitAt);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentEntry>> ListPaymentsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var payments = await db.Payments.AsNoTracking()
            .Where(payment => payment.PaidAt >= from && payment.PaidAt < to)
            .Select(payment => new { payment.Amount, payment.PaidAt })
            .ToListAsync(cancellationToken);

        return payments.Select(payment => new PaymentEntry(payment.Amount.Amount, payment.PaidAt)).ToList();
    }

    public async Task<IReadOnlyList<SessionLoadEntry>> ListSessionLoadAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var sessions = await db.TrainingSessions.AsNoTracking()
            .Where(session => session.Status == SessionStatus.Scheduled && session.Slot.Start >= from && session.Slot.Start < to)
            .ToListAsync(cancellationToken);

        var trainerIds = sessions.Select(session => session.TrainerId).Distinct().ToList();
        var roomIds = sessions.Select(session => session.RoomId).Distinct().ToList();

        var trainerNames = (await db.Trainers.AsNoTracking()
                .Where(trainer => trainerIds.Contains(trainer.Id))
                .Select(trainer => new { trainer.Id, trainer.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(trainer => trainer.Id, trainer => trainer.Name.FullName);

        var roomNames = await db.Rooms.AsNoTracking()
            .Where(room => roomIds.Contains(room.Id))
            .ToDictionaryAsync(room => room.Id, room => room.Name, cancellationToken);

        return sessions
            .Select(session => new SessionLoadEntry(
                session.TrainerId,
                trainerNames.GetValueOrDefault(session.TrainerId, ""),
                session.RoomId,
                roomNames.GetValueOrDefault(session.RoomId, ""),
                session.Slot.Start,
                session.Slot.End,
                session.Capacity,
                session.Bookings.Where(booking => booking.IsActive).Select(booking => booking.ClientId).ToList()))
            .ToList();
    }
}
