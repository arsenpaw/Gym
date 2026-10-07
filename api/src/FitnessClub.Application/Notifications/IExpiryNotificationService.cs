namespace FitnessClub.Application.Notifications;

public interface IExpiryNotificationService
{
    Task<int> CreateDueNoticesAsync(CancellationToken cancellationToken);

    Task<NotificationDeliveryResult> SendPendingAsync(CancellationToken cancellationToken);

    Task<NotificationRunResponse> RunAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationResponse>> ListAsync(NotificationListRequest request, CancellationToken cancellationToken);

    Task RetryAsync(Guid id, CancellationToken cancellationToken);
}
