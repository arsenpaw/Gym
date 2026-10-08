using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Notifications;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;

namespace FitnessClub.IntegrationTests.Persistence;

public class NotificationRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    [Fact]
    public async Task Notification_round_trips_and_is_found_by_membership()
    {
        var client = Client.Register(PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), EmailAddress.Create("olena@example.com"), Now);
        var payment = client.PurchaseMembership(await SavedPlanAsync(), Today, PaymentMethod.Cash, Now);
        var membership = client.Memberships.Single(m => m.Id == payment.MembershipId);
        var notification = Notification.MembershipExpiring(client, membership, TestData.Content(), Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));
        await SaveAsync<INotificationRepository>(notifications => notifications.Add(notification));

        Assert.True(await ReadAsync<INotificationRepository, bool>(
            n => n.ExistsForMembershipAsync(membership.Id, NotificationType.MembershipExpiring, Ct)));
        Assert.Contains(await ReadAsync<INotificationRepository, IReadOnlyList<Notification>>(n => n.ListPendingAsync(Ct)), n => n.Id == notification.Id);

        await ChangeAsync<INotificationRepository>(async notifications =>
            (await notifications.GetByIdAsync(notification.Id, Ct))!.MarkSent(Now.AddMinutes(1)));

        var loaded = await ReadAsync<INotificationRepository, Notification?>(n => n.GetByIdAsync(notification.Id, Ct));
        Assert.Equal(NotificationStatus.Sent, loaded!.Status);
        Assert.Equal(NotificationChannel.Email, loaded.Channel);
        Assert.Equal("Your membership expires soon", loaded.Subject);
        Assert.Equal("<p>Hello</p>", loaded.HtmlBody);
        Assert.DoesNotContain(await ReadAsync<INotificationRepository, IReadOnlyList<Notification>>(n => n.ListPendingAsync(Ct)), n => n.Id == notification.Id);
    }

    [Fact]
    public async Task ListForClientAsync_returns_only_that_clients_notifications_newest_first()
    {
        var olena = Client.Register(PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), EmailAddress.Create("olena.list@example.com"), Now);
        var ivan = Client.Register(PersonName.Create("Ivan", "Koval", null), new DateOnly(1990, 1, 2), PhoneNumber.Create(UniquePhone()), EmailAddress.Create("ivan.list@example.com"), Now);
        var older = Notification.Promotion(olena, TestData.Content(), Now);
        var newer = Notification.Promotion(olena, TestData.Content(html: null), Now.AddHours(1));
        var other = Notification.Promotion(ivan, TestData.Content(), Now);
        await SaveAsync<IClientRepository>(clients =>
        {
            clients.Add(olena);
            clients.Add(ivan);
        });
        await SaveAsync<INotificationRepository>(notifications =>
        {
            notifications.Add(older);
            notifications.Add(newer);
            notifications.Add(other);
        });

        var list = await ReadAsync<INotificationRepository, IReadOnlyList<Notification>>(n => n.ListForClientAsync(olena.Id, Ct));

        Assert.Equal([newer.Id, older.Id], list.Select(n => n.Id));
        Assert.Null(list[0].HtmlBody);
    }
}
