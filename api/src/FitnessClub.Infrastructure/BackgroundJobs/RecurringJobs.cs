using Hangfire;

namespace FitnessClub.Infrastructure.BackgroundJobs;

internal static class RecurringJobs
{
    public static void Register(IRecurringJobManager recurringJobs)
    {
        recurringJobs.AddOrUpdate<ExpiryNotificationJob>(
            ExpiryNotificationJob.Id,
            job => job.RunAsync(CancellationToken.None),
            Cron.Daily(8),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
    }
}
