using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Application.Notifications;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.Application.ClientMessages;

internal sealed class ClientMessageService(
    IClientRepository clients,
    INotificationRepository notifications,
    INotificationSender sender,
    IEmailTemplates templates,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IClientMessageService
{
    public const int PromotionDiscountPercent = 10;

    public async Task<ClientMessagePreviewResponse> PreviewAsync(Guid clientId, ClientMessageRequest request, CancellationToken cancellationToken)
    {
        var template = Parse(request);
        var notification = Compose(await GetClientAsync(clientId, cancellationToken), template, timeProvider.GetLocalNow());
        return new ClientMessagePreviewResponse(template.ToString(), notification.Recipient, notification.Subject, notification.HtmlBody!);
    }

    public async Task<NotificationResponse> SendAsync(Guid clientId, ClientMessageRequest request, CancellationToken cancellationToken)
    {
        var notification = Compose(await GetClientAsync(clientId, cancellationToken), Parse(request), timeProvider.GetLocalNow());
        notifications.Add(notification);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await sender.SendAsync(notification, cancellationToken);
            notification.MarkSent(timeProvider.GetLocalNow());
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            notification.MarkFailed(string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NotificationResponse.FromEntity(notification);
    }

    public async Task<IReadOnlyList<NotificationResponse>> ListAsync(Guid clientId, CancellationToken cancellationToken)
    {
        await GetClientAsync(clientId, cancellationToken);
        var list = await notifications.ListForClientAsync(clientId, cancellationToken);
        return list.Select(NotificationResponse.FromEntity).ToList();
    }

    private Notification Compose(Client client, MessageTemplate template, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.DateTime);
        switch (template)
        {
            case MessageTemplate.ExpiryReminder:
                var membership = client.ActiveMembershipOn(today)
                    ?? throw new DomainException("The client has no active membership to remind about.");
                return Notification.ExpiryReminder(
                    client, membership, templates.ExpiryReminder(ExpiryReminderEmail.For(client, membership, today)), now);
            case MessageTemplate.Promotion:
                var validUntil = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                return Notification.Promotion(
                    client, templates.Promotion(new PromotionEmail(client.Name.FirstName, PromotionDiscountPercent, validUntil)), now);
            default:
                throw new DomainException($"Unknown message template '{template}'.");
        }
    }

    private static MessageTemplate Parse(ClientMessageRequest request) =>
        Enum.TryParse<MessageTemplate>(request.Template, ignoreCase: true, out var template) && Enum.IsDefined(template)
            ? template
            : throw new DomainException("Template must be ExpiryReminder or Promotion.");

    private async Task<Client> GetClientAsync(Guid id, CancellationToken cancellationToken) =>
        await clients.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Client '{id}' was not found.");
}
