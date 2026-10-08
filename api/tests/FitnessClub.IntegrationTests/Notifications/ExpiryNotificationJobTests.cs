using FitnessClub.Domain.Notifications;
using FitnessClub.IntegrationTests.Infrastructure;
using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Notifications;

[Collection(HangfireStorageCollection.Name)]
public class ExpiryNotificationJobTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private const string JobId = "membership-expiry-notifications";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Startup_registers_the_daily_expiry_job_in_club_local_time()
    {
        using var connection = factory.Services.GetRequiredService<JobStorage>().GetConnection();

        var job = Assert.Single(connection.GetRecurringJobs(), job => job.Id == JobId);

        Assert.Equal(Cron.Daily(8), job.Cron);
        Assert.Equal(TimeZoneInfo.Local.Id, job.TimeZoneId);
        Assert.Equal("ExpiryNotificationJob", job.Job.Type.Name);
        Assert.Equal("RunAsync", job.Job.Method.Name);
    }

    [Fact]
    public async Task Triggered_job_creates_and_sends_the_notice()
    {
        var membership = await factory.ClientWithMembershipEndingInAsync(2);
        var recurringJobs = (IRecurringJobManagerV2)factory.Services.GetRequiredService<IRecurringJobManager>();

        var jobId = recurringJobs.TriggerJob(JobId);

        Assert.Equal(SucceededState.StateName, await WaitForFinalStateAsync(jobId));
        var notice = Assert.Single(await factory.NotificationsAsync(), n => n.MembershipId == membership.Id);
        Assert.Equal(NotificationStatus.Sent, notice.Status);
    }

    private async Task<string?> WaitForFinalStateAsync(string jobId)
    {
        var monitoring = factory.Services.GetRequiredService<JobStorage>().GetMonitoringApi();
        for (var attempt = 0; attempt < 150; attempt++)
        {
            var state = monitoring.JobDetails(jobId)?.History.FirstOrDefault()?.StateName;
            if (state == SucceededState.StateName || state == FailedState.StateName)
                return state;

            await Task.Delay(100, Ct);
        }

        return null;
    }
}
