using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Domain.Training;

public interface ISessionScheduler
{
    Task<TrainingSession> ScheduleAsync(
        string title,
        SessionType type,
        Trainer trainer,
        Room room,
        TimeSlot slot,
        int capacity,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Booking> BookAsync(TrainingSession session, Client client, DateTimeOffset now, CancellationToken cancellationToken);
}
