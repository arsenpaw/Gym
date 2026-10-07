using FitnessClub.Application.Clients;
using FitnessClub.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Receptionist}")]
public sealed class ClientsController(IClientService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ClientSummaryResponse>> List(CancellationToken cancellationToken) =>
        service.ListAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ClientDetailsResponse> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ClientDetailsResponse>> Register(ClientRequest request, CancellationToken cancellationToken)
    {
        var client = await service.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = client.Id }, client);
    }

    [HttpPut("{id:guid}")]
    public Task<ClientDetailsResponse> Update(Guid id, ClientRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/memberships")]
    public async Task<ActionResult<MembershipResponse>> PurchaseMembership(
        Guid id, PurchaseMembershipRequest request, CancellationToken cancellationToken)
    {
        var membership = await service.PurchaseMembershipAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, membership);
    }

    [HttpPost("{id:guid}/memberships/{membershipId:guid}/cancel")]
    public async Task<IActionResult> CancelMembership(Guid id, Guid membershipId, CancellationToken cancellationToken)
    {
        await service.CancelMembershipAsync(id, membershipId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/visits")]
    public async Task<ActionResult<VisitResponse>> CheckIn(Guid id, CancellationToken cancellationToken)
    {
        var visit = await service.CheckInAsync(id, cancellationToken);
        return CreatedAtAction(nameof(ListVisits), new { id }, visit);
    }

    [HttpGet("{id:guid}/visits")]
    public Task<IReadOnlyList<VisitResponse>> ListVisits(Guid id, CancellationToken cancellationToken) =>
        service.ListVisitsAsync(id, cancellationToken);
}
