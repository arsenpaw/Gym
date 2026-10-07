namespace FitnessClub.Application.Reports;

public interface IReportService
{
    Task<ClientActivityReportResponse> GetClientActivityAsync(DateRangeRequest request, CancellationToken cancellationToken);

    Task<RevenueReportResponse> GetRevenueAsync(RevenueReportRequest request, CancellationToken cancellationToken);

    Task<LoadReportResponse> GetLoadAsync(DateRangeRequest request, CancellationToken cancellationToken);
}
