using FitnessClub.Application.Common;
using FitnessClub.Application.Trainers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/trainers")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Receptionist}")]
public sealed class TrainersController(ITrainerService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TrainerSummaryResponse>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        service.ListAsync(includeInactive, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<TrainerResponse> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<TrainerResponse>> Hire(TrainerRequest request, CancellationToken cancellationToken)
    {
        var trainer = await service.HireAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = trainer.Id }, trainer);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public Task<TrainerResponse> UpdateProfile(Guid id, TrainerRequest request, CancellationToken cancellationToken) =>
        service.UpdateProfileAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await service.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await service.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/working-hours")]
    [Authorize(Roles = Roles.Admin)]
    public Task<TrainerResponse> SetWorkingHours(Guid id, [NoNullItems] List<WorkingHoursRequest> request, CancellationToken cancellationToken) =>
        service.SetWorkingHoursAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/clients/{clientId:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AssignClient(Guid id, Guid clientId, CancellationToken cancellationToken)
    {
        await service.AssignClientAsync(id, clientId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/clients/{clientId:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnassignClient(Guid id, Guid clientId, CancellationToken cancellationToken)
    {
        await service.UnassignClientAsync(id, clientId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/identity")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LinkIdentity(Guid id, LinkIdentityRequest request, CancellationToken cancellationToken)
    {
        await service.LinkIdentityAsync(id, request, cancellationToken);
        return NoContent();
    }
}
