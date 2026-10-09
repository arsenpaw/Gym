using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Notifications;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Notifications;

internal static class NotificationSeeder
{
    private const int ValidityDays = 30;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<Membership> ClientWithMembershipEndingInAsync(this FitnessClubApiFactory factory, int days)
    {
        var startedDaysAgo = ValidityDays - 1 - days;
        var purchasedAt = TimeProvider.System.GetLocalNow().AddDays(-startedDaysAgo);
        var (client, membership, plan) = NewClient(purchasedAt);

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IMembershipPlanRepository>().Add(plan);
        scope.ServiceProvider.GetRequiredService<IClientRepository>().Add(client);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return membership;
    }

    public static async Task<Notification> NotificationAsync(this FitnessClubApiFactory factory, string? failureReason = null)
    {
        var now = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
        var (client, membership, plan) = NewClient(now);
        var notification = Notification.MembershipExpiring(client, membership, TestData.Content(), now);
        if (failureReason is not null)
            notification.MarkFailed(failureReason);

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IMembershipPlanRepository>().Add(plan);
        scope.ServiceProvider.GetRequiredService<IClientRepository>().Add(client);
        scope.ServiceProvider.GetRequiredService<INotificationRepository>().Add(notification);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return notification;
    }

    public static async Task<Client> ClientWithMembershipAsync(this FitnessClubApiFactory factory)
    {
        var (client, _, plan) = NewClient(TimeProvider.System.GetLocalNow());

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IMembershipPlanRepository>().Add(plan);
        scope.ServiceProvider.GetRequiredService<IClientRepository>().Add(client);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return client;
    }

    public static async Task<IReadOnlyList<Notification>> NotificationsAsync(this FitnessClubApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<INotificationRepository>().ListAsync(null, Ct);
    }

    private static (Client Client, Membership Membership, MembershipPlan Plan) NewClient(DateTimeOffset now)
    {
        var plan = MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(800m), ValidityDays, null);
        var client = Client.Register(
            PersonName.Create("Olena", "Shevchenko", null),
            new DateOnly(1995, 3, 14),
            EmailAddress.Create($"{Guid.NewGuid():N}@example.com"),
            PhoneNumber.Create($"+380{Random.Shared.NextInt64(100_000_000, 999_999_999)}"),
            now);
        var payment = client.PurchaseMembership(plan, DateOnly.FromDateTime(now.DateTime), PaymentMethod.Cash, now);
        return (client, client.Memberships.Single(m => m.Id == payment.MembershipId), plan);
    }
}
