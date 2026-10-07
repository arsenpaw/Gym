using FitnessClub.Application.Common;
using FitnessClub.Application.Training;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Receptionist},{Roles.Trainer}")]
public sealed class SessionsController(ITrainingSessionService service) : ControllerBase
{
    private const string Staff = $"{Roles.Admin},{Roles.Receptionist}";
    private const string SubjectClaim = "sub";

    [HttpGet]
    public Task<IReadOnlyList<SessionSummaryResponse>> List([FromQuery] SessionPeriod period, CancellationToken cancellationToken) =>
        service.ListAsync(period.From.GetValueOrDefault(), period.To.GetValueOrDefault(), cancellationToken);

    [HttpGet("mine")]
    [Authorize(Roles = Roles.Trainer)]
    public Task<IReadOnlyList<SessionSummaryResponse>> Mine([FromQuery] SessionPeriod period, CancellationToken cancellationToken) =>
        service.ListForTrainerAsync(
            User.FindFirst(SubjectClaim)?.Value ?? "", period.From.GetValueOrDefault(), period.To.GetValueOrDefault(), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<SessionResponse> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = Staff)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<SessionResponse>> Schedule(ScheduleSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await service.ScheduleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Staff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await service.CancelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/bookings")]
    [Authorize(Roles = Staff)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<BookingResponse>> Book(Guid id, BookSessionRequest request, CancellationToken cancellationToken)
    {
        var booking = await service.BookAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, booking);
    }

    [HttpPost("{id:guid}/bookings/{clientId:guid}/cancel")]
    [Authorize(Roles = Staff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelBooking(Guid id, Guid clientId, CancellationToken cancellationToken)
    {
        await service.CancelBookingAsync(id, clientId, cancellationToken);
        return NoContent();
    }
}
