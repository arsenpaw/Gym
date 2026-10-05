using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class TrainingSessionRepository(FitnessClubDbContext db)
    : Repository<TrainingSession>(db), ITrainingSessionRepository
{
    public Task<bool> TrainerHasSessionDuringAsync(Guid trainerId, TimeSlot slot, CancellationToken cancellationToken) =>
        Scheduled().AnyAsync(
            s => s.TrainerId == trainerId && s.Slot.Start < slot.End && slot.Start < s.Slot.End,
            cancellationToken);

    public Task<bool> RoomIsBookedDuringAsync(Guid roomId, TimeSlot slot, CancellationToken cancellationToken) =>
        Scheduled().AnyAsync(
            s => s.RoomId == roomId && s.Slot.Start < slot.End && slot.Start < s.Slot.End,
            cancellationToken);

    public Task<bool> ClientHasBookingDuringAsync(Guid clientId, TimeSlot slot, CancellationToken cancellationToken) =>
        Scheduled().AnyAsync(
            s => s.Slot.Start < slot.End && slot.Start < s.Slot.End
                && s.Bookings.Any(b => b.ClientId == clientId && b.CancelledAt == null),
            cancellationToken);

    public async Task<IReadOnlyList<TrainingSession>> ListStartingBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await Set
            .Where(s => s.Slot.Start >= from && s.Slot.Start < to)
            .OrderBy(s => s.Slot.Start)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrainingSession>> ListForTrainerStartingBetweenAsync(
        Guid trainerId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await Set
            .Where(s => s.TrainerId == trainerId && s.Slot.Start >= from && s.Slot.Start < to)
            .OrderBy(s => s.Slot.Start)
            .ToListAsync(cancellationToken);

    private IQueryable<TrainingSession> Scheduled() => Set.Where(s => s.Status == SessionStatus.Scheduled);
}
