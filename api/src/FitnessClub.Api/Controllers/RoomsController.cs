using FitnessClub.Application.Common;
using FitnessClub.Application.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/rooms")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Receptionist},{Roles.Trainer}")]
public sealed class RoomsController(IRoomService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RoomResponse>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        service.ListAsync(includeInactive, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<RoomResponse> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<RoomResponse>> Create(RoomRequest request, CancellationToken cancellationToken)
    {
        var room = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = room.Id }, room);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public Task<RoomResponse> Update(Guid id, RoomRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

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
}
