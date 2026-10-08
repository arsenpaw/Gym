using FitnessClub.Application.ClientMessages;
using FitnessClub.Application.Common;
using FitnessClub.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/clients/{id:guid}/messages")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Receptionist}")]
public sealed class ClientMessagesController(IClientMessageService service) : ControllerBase
{
    [HttpGet("preview")]
    public Task<ClientMessagePreviewResponse> Preview(Guid id, [FromQuery] ClientMessageRequest request, CancellationToken cancellationToken) =>
        service.PreviewAsync(id, request, cancellationToken);

    [HttpPost]
    public Task<NotificationResponse> Send(Guid id, ClientMessageRequest request, CancellationToken cancellationToken) =>
        service.SendAsync(id, request, cancellationToken);

    [HttpGet]
    public Task<IReadOnlyList<NotificationResponse>> List(Guid id, CancellationToken cancellationToken) =>
        service.ListAsync(id, cancellationToken);
}
