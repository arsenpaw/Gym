using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Training;

public interface ITrainingSessionRepository : IRepository<TrainingSession>
{
    Task<bool> TrainerHasSessionDuringAsync(Guid trainerId, TimeSlot slot, CancellationToken cancellationToken);

    Task<bool> RoomIsBookedDuringAsync(Guid roomId, TimeSlot slot, CancellationToken cancellationToken);

    Task<bool> ClientHasBookingDuringAsync(Guid clientId, TimeSlot slot, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainingSession>> ListStartingBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainingSession>> ListForTrainerStartingBetweenAsync(
        Guid trainerId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
