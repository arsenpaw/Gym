using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;

namespace FitnessClub.Application.Training;

internal sealed class TrainingSessionService(
    ITrainingSessionRepository sessions,
    ITrainerRepository trainers,
    IRoomRepository rooms,
    IClientRepository clients,
    ISessionScheduler scheduler,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ITrainingSessionService
{
    public async Task<IReadOnlyList<SessionSummaryResponse>> ListAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var list = await sessions.ListStartingBetweenAsync(from, to, cancellationToken);
        return list.Select(SessionSummaryResponse.FromEntity).ToList();
    }

    public async Task<IReadOnlyList<SessionSummaryResponse>> ListForTrainerAsync(
        string identityUserId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var trainer = string.IsNullOrWhiteSpace(identityUserId)
            ? null
            : await trainers.GetByIdentityUserIdAsync(identityUserId.Trim(), cancellationToken);

        if (trainer is null)
            throw new NotFoundException("No trainer profile is linked to the current user.");

        var list = await sessions.ListForTrainerStartingBetweenAsync(trainer.Id, from, to, cancellationToken);
        return list.Select(SessionSummaryResponse.FromEntity).ToList();
    }

    public async Task<SessionResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        SessionResponse.FromEntity(await FindAsync(id, cancellationToken));

    public async Task<SessionResponse> ScheduleAsync(ScheduleSessionRequest request, CancellationToken cancellationToken)
    {
        var trainerId = request.TrainerId.GetValueOrDefault();
        var roomId = request.RoomId.GetValueOrDefault();
        var trainer = await trainers.GetByIdAsync(trainerId, cancellationToken)
            ?? throw new NotFoundException($"Trainer '{trainerId}' was not found.");
        var room = await rooms.GetByIdAsync(roomId, cancellationToken)
            ?? throw new NotFoundException($"Room '{roomId}' was not found.");

        var slot = TimeSlot.Create(request.Start.GetValueOrDefault(), request.End.GetValueOrDefault());
        var session = await scheduler.ScheduleAsync(
            request.Title, request.Type.GetValueOrDefault(), trainer, room, slot, request.Capacity, Now, cancellationToken);

        sessions.Add(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SessionResponse.FromEntity(session);
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await FindAsync(id, cancellationToken);
        session.Cancel(Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<BookingResponse> BookAsync(Guid sessionId, BookSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await FindAsync(sessionId, cancellationToken);
        var clientId = request.ClientId.GetValueOrDefault();
        var client = await clients.GetByIdAsync(clientId, cancellationToken)
            ?? throw new NotFoundException($"Client '{clientId}' was not found.");

        var booking = await scheduler.BookAsync(session, client, Now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BookingResponse.FromEntity(booking);
    }

    public async Task CancelBookingAsync(Guid sessionId, Guid clientId, CancellationToken cancellationToken)
    {
        var session = await FindAsync(sessionId, cancellationToken);
        session.CancelBooking(clientId, Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private DateTimeOffset Now => timeProvider.GetLocalNow();

    private async Task<TrainingSession> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await sessions.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Training session '{id}' was not found.");
}
