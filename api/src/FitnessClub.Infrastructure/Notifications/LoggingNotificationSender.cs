using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace FitnessClub.Infrastructure.Notifications;

internal sealed partial class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        LogNotification(notification.Channel, notification.Recipient, notification.Subject, notification.Message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending {Channel} notification to {Recipient}: {Subject}. {Message}")]
    private partial void LogNotification(NotificationChannel channel, string recipient, string subject, string message);
}
