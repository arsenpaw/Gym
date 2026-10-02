using Hangfire;

namespace FitnessClub.Infrastructure.BackgroundJobs;

public static class RecurringJobs
{
    // Single place where recurring jobs are scheduled. Sub-project 4 adds the membership expiry job here.
    public static void Register(IRecurringJobManager recurringJobs)
    {
    }
}
