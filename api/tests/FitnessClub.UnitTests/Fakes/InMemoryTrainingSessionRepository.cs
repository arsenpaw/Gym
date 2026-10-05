using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Training;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryTrainingSessionRepository : ITrainingSessionRepository
{
    private readonly List<TrainingSession> _sessions = [];

    public Task<TrainingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));

    public void Add(TrainingSession aggregate) => _sessions.Add(aggregate);

    public Task<bool> TrainerHasSessionDuringAsync(Guid trainerId, TimeSlot slot, CancellationToken cancellationToken) =>
        Task.FromResult(Scheduled().Any(s => s.TrainerId == trainerId && s.Slot.Overlaps(slot)));

    public Task<bool> RoomIsBookedDuringAsync(Guid roomId, TimeSlot slot, CancellationToken cancellationToken) =>
        Task.FromResult(Scheduled().Any(s => s.RoomId == roomId && s.Slot.Overlaps(slot)));

    public Task<bool> ClientHasBookingDuringAsync(Guid clientId, TimeSlot slot, CancellationToken cancellationToken) =>
        Task.FromResult(Scheduled().Any(s => s.Slot.Overlaps(slot) && s.Bookings.Any(b => b.IsActive && b.ClientId == clientId)));

    public Task<IReadOnlyList<TrainingSession>> ListStartingBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TrainingSession>>(
            _sessions.Where(s => s.Slot.Start >= from && s.Slot.Start < to).OrderBy(s => s.Slot.Start).ToList());

    public Task<IReadOnlyList<TrainingSession>> ListForTrainerStartingBetweenAsync(
        Guid trainerId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TrainingSession>>(
            _sessions.Where(s => s.TrainerId == trainerId && s.Slot.Start >= from && s.Slot.Start < to).OrderBy(s => s.Slot.Start).ToList());

    private IEnumerable<TrainingSession> Scheduled() => _sessions.Where(s => s.Status == SessionStatus.Scheduled);
}
