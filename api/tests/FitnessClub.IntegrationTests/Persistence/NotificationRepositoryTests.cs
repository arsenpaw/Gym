using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Notifications;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Persistence;

public class NotificationRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    [Fact]
    public async Task Notification_round_trips_and_is_found_by_membership()
    {
        var client = Client.Register(PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), null, Now);
        var payment = client.PurchaseMembership(await SavedPlanAsync(), Today, PaymentMethod.Cash, Now);
        var membership = client.Memberships.Single(m => m.Id == payment.MembershipId);
        var notification = Notification.MembershipExpiring(client, membership, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));
        await SaveAsync<INotificationRepository>(notifications => notifications.Add(notification));

        Assert.True(await ReadAsync<INotificationRepository, bool>(
            n => n.ExistsForMembershipAsync(membership.Id, NotificationType.MembershipExpiring, Ct)));
        Assert.Contains(await ReadAsync<INotificationRepository, IReadOnlyList<Notification>>(n => n.ListPendingAsync(Ct)), n => n.Id == notification.Id);

        await ChangeAsync<INotificationRepository>(async notifications =>
            (await notifications.GetByIdAsync(notification.Id, Ct))!.MarkSent(Now.AddMinutes(1)));

        var loaded = await ReadAsync<INotificationRepository, Notification?>(n => n.GetByIdAsync(notification.Id, Ct));
        Assert.Equal(NotificationStatus.Sent, loaded!.Status);
        Assert.Equal(NotificationChannel.Sms, loaded.Channel);
        Assert.DoesNotContain(await ReadAsync<INotificationRepository, IReadOnlyList<Notification>>(n => n.ListPendingAsync(Ct)), n => n.Id == notification.Id);
    }
}
