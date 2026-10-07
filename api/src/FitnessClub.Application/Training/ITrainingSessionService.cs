namespace FitnessClub.Application.Training;

public interface ITrainingSessionService
{
    Task<IReadOnlyList<SessionSummaryResponse>> ListAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionSummaryResponse>> ListForTrainerAsync(
        string identityUserId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<SessionResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<SessionResponse> ScheduleAsync(ScheduleSessionRequest request, CancellationToken cancellationToken);

    Task CancelAsync(Guid id, CancellationToken cancellationToken);

    Task<BookingResponse> BookAsync(Guid sessionId, BookSessionRequest request, CancellationToken cancellationToken);

    Task CancelBookingAsync(Guid sessionId, Guid clientId, CancellationToken cancellationToken);
}
