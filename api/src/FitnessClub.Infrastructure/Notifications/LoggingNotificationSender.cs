using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace FitnessClub.Infrastructure.Notifications;

internal sealed partial class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(NotificationChannel channel, string recipient, string message, CancellationToken cancellationToken)
    {
        LogNotification(channel, recipient, message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending {Channel} notification to {Recipient}: {Message}")]
    private partial void LogNotification(NotificationChannel channel, string recipient, string message);
}
