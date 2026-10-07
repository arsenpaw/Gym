using FitnessClub.Application.Common;
using FitnessClub.Application.MembershipPlans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/membership-plans")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Receptionist}")]
public sealed class MembershipPlansController(IMembershipPlanService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<MembershipPlanResponse>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        service.ListAsync(includeInactive, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<MembershipPlanResponse> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<MembershipPlanResponse>> Create(MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = plan.Id }, plan);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public Task<MembershipPlanResponse> Update(Guid id, MembershipPlanRequest request, CancellationToken cancellationToken) =>
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
