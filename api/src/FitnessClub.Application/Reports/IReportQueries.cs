namespace FitnessClub.Application.Reports;

public interface IReportQueries
{
    Task<IReadOnlyList<ClientActivityItem>> ListClientActivityAsync(
        DateTimeOffset visitsFrom, DateTimeOffset visitsTo, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentEntry>> ListPaymentsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionLoadEntry>> ListSessionLoadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
