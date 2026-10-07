using FitnessClub.Application.Notifications;
using Hangfire;

namespace FitnessClub.Infrastructure.BackgroundJobs;

internal sealed class ExpiryNotificationJob(IExpiryNotificationService notifications)
{
    public const string Id = "membership-expiry-notifications";

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public Task RunAsync(CancellationToken cancellationToken) => notifications.RunAsync(cancellationToken);
}
