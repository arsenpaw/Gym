using FitnessClub.Application.Common;
using FitnessClub.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.Admin)]
public sealed class ReportsController(IReportService service) : ControllerBase
{
    [HttpGet("client-activity")]
    public Task<ClientActivityReportResponse> ClientActivity([FromQuery] DateRangeRequest request, CancellationToken cancellationToken) =>
        service.GetClientActivityAsync(request, cancellationToken);

    [HttpGet("revenue")]
    public Task<RevenueReportResponse> Revenue([FromQuery] RevenueReportRequest request, CancellationToken cancellationToken) =>
        service.GetRevenueAsync(request, cancellationToken);

    [HttpGet("load")]
    public Task<LoadReportResponse> Load([FromQuery] DateRangeRequest request, CancellationToken cancellationToken) =>
        service.GetLoadAsync(request, cancellationToken);
}
