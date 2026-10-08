using FitnessClub.Application.Notifications;

namespace FitnessClub.Application.ClientMessages;

public interface IClientMessageService
{
    Task<ClientMessagePreviewResponse> PreviewAsync(Guid clientId, ClientMessageRequest request, CancellationToken cancellationToken);

    Task<NotificationResponse> SendAsync(Guid clientId, ClientMessageRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationResponse>> ListAsync(Guid clientId, CancellationToken cancellationToken);
}
