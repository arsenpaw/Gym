using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Domain.Training;

public sealed class SessionScheduler(ITrainingSessionRepository sessions) : ISessionScheduler
{
    public async Task<TrainingSession> ScheduleAsync(
        string title,
        SessionType type,
        Trainer trainer,
        Room room,
        TimeSlot slot,
        int capacity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = TrainingSession.Create(title, type, trainer, room, slot, capacity, now);

        if (await sessions.TrainerHasSessionDuringAsync(trainer.Id, slot, cancellationToken))
            throw new DomainException("The trainer already has a session at that time.");

        if (await sessions.RoomIsBookedDuringAsync(room.Id, slot, cancellationToken))
            throw new DomainException($"Room '{room.Name}' is already booked at that time.");

        return session;
    }

    public async Task<Booking> BookAsync(TrainingSession session, Client client, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await sessions.ClientHasBookingDuringAsync(client.Id, session.Slot, cancellationToken))
            throw new DomainException("The client already has a booking at that time.");

        return session.Book(client, now);
    }
}
