using FitnessClub.Application.Common;
using FitnessClub.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = Roles.Admin)]
public sealed class NotificationsController(IExpiryNotificationService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<NotificationResponse>> List([FromQuery] NotificationListRequest request, CancellationToken cancellationToken) =>
        service.ListAsync(request, cancellationToken);

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        await service.RetryAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("run")]
    public Task<NotificationRunResponse> Run(CancellationToken cancellationToken) =>
        service.RunAsync(cancellationToken);
}
