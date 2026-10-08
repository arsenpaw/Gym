using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.Application.Notifications;

internal sealed class ExpiryNotificationService(
    IClientRepository clients,
    INotificationRepository notifications,
    INotificationSender sender,
    IEmailTemplates templates,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ExpiryNotificationOptions options) : IExpiryNotificationService
{
    public async Task<int> CreateDueNoticesAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        var endsBy = today.AddDays(options.ExpiryNoticeDays);
        var created = 0;

        foreach (var client in await clients.ListWithMembershipsEndingBetweenAsync(today, endsBy, cancellationToken))
        {
            foreach (var membership in client.MembershipsNeedingExpiryNotice(today, endsBy).Where(OutlastsNoticeWindow))
            {
                if (await notifications.ExistsForMembershipAsync(membership.Id, NotificationType.MembershipExpiring, cancellationToken))
                    continue;

                notifications.Add(Notification.MembershipExpiring(
                    client, membership, templates.ExpiryReminder(ExpiryReminderEmail.For(client, membership, today)), now));
                created++;
            }
        }

        if (created > 0)
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task<NotificationDeliveryResult> SendPendingAsync(CancellationToken cancellationToken)
    {
        var sent = 0;
        var failed = 0;

        foreach (var notification in await notifications.ListPendingAsync(cancellationToken))
        {
            try
            {
                await sender.SendAsync(notification.Channel, notification.Recipient, notification.Message, cancellationToken);
                notification.MarkSent(timeProvider.GetLocalNow());
                sent++;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                notification.MarkFailed(string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message);
                failed++;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new NotificationDeliveryResult(sent, failed);
    }

    public async Task<NotificationRunResponse> RunAsync(CancellationToken cancellationToken)
    {
        var created = await CreateDueNoticesAsync(cancellationToken);
        var delivery = await SendPendingAsync(cancellationToken);
        return new NotificationRunResponse(created, delivery.Sent, delivery.Failed);
    }

    public async Task<IReadOnlyList<NotificationResponse>> ListAsync(NotificationListRequest request, CancellationToken cancellationToken)
    {
        NotificationStatus? status = request.Status is null ? null : Enum.Parse<NotificationStatus>(request.Status, ignoreCase: true);
        var list = await notifications.ListAsync(status, cancellationToken);
        return list.Select(NotificationResponse.FromEntity).ToList();
    }

    public async Task RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await notifications.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Notification '{id}' was not found.");

        notification.Retry();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private bool OutlastsNoticeWindow(Membership membership) =>
        membership.EndsOn.DayNumber - membership.StartsOn.DayNumber >= options.ExpiryNoticeDays;
}
