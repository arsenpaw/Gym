using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Visits;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Persistence;

public class ClientRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    private static Client NewClient(bool withPhone = true) =>
        Client.Register(
            PersonName.Create("Olena", "Shevchenko", "Petrivna"),
            new DateOnly(1995, 3, 14),
            EmailAddress.Create(TestData.UniqueEmail()),
            withPhone ? PhoneNumber.Create(UniquePhone()) : null,
            Now);

    [Fact]
    public async Task Client_round_trips_with_value_objects_and_memberships()
    {
        var client = NewClient();
        var payment = client.PurchaseMembership(await SavedPlanAsync(visitLimit: 8), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        var loaded = await ReadAsync<IClientRepository, Client?>(clients => clients.GetByIdAsync(client.Id, Ct));

        Assert.NotNull(loaded);
        Assert.Equal(client.Name, loaded.Name);
        Assert.Equal(client.Phone, loaded.Phone);
        Assert.Equal(client.Email, loaded.Email);
        var loadedMembership = Assert.Single(loaded.Memberships);
        Assert.Equal(payment.MembershipId, loadedMembership.Id);
        Assert.Equal(Today.AddDays(29), loadedMembership.EndsOn);
        Assert.Equal(8, loadedMembership.RemainingVisits);
    }

    [Fact]
    public async Task Membership_bought_on_loaded_client_is_inserted()
    {
        var client = NewClient(withPhone: false);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        await ChangeAsync<IClientRepository>(async clients =>
        {
            var loaded = await clients.GetByIdAsync(client.Id, Ct);
            loaded!.PurchaseMembership(await SavedPlanAsync(), Today, PaymentMethod.Card, Now);
        });

        var reloaded = await ReadAsync<IClientRepository, Client?>(clients => clients.GetByIdAsync(client.Id, Ct));
        Assert.Single(reloaded!.Memberships);
        Assert.Equal(client.Email, reloaded.Email);
        Assert.Null(reloaded.Phone);
    }

    [Fact]
    public async Task Check_in_saves_visit_and_used_visit_in_one_unit_of_work()
    {
        var client = NewClient();
        client.PurchaseMembership(await SavedPlanAsync(visitLimit: 5), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        await InUnitOfWorkAsync(async services =>
        {
            var loaded = await services.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
            services.GetRequiredService<IVisitRepository>().Add(loaded!.CheckIn(Now));
        });

        var reloaded = await ReadAsync<IClientRepository, Client?>(clients => clients.GetByIdAsync(client.Id, Ct));
        var history = await ReadAsync<IVisitRepository, IReadOnlyList<Visit>>(visits => visits.ListForClientAsync(client.Id, 0, 10, Ct));
        Assert.Equal(4, reloaded!.Memberships.Single().RemainingVisits);
        Assert.Equal(Now, Assert.Single(history).CheckedInAt);
    }

    [Fact]
    public async Task EmailExistsAsync_matches_normalized_email()
    {
        var client = NewClient();
        await SaveAsync<IClientRepository>(clients => clients.Add(client));
        var typed = EmailAddress.Create(client.Email.Value.ToUpperInvariant());

        Assert.True(await ReadAsync<IClientRepository, bool>(clients => clients.EmailExistsAsync(typed, null, Ct)));
        Assert.False(await ReadAsync<IClientRepository, bool>(clients => clients.EmailExistsAsync(typed, client.Id, Ct)));
    }

    [Fact]
    public async Task Visits_are_paged_newest_first_with_a_total_count()
    {
        var client = NewClient();
        client.PurchaseMembership(await SavedPlanAsync(), Today, PaymentMethod.Cash, Now);
        var visits = Enumerable.Range(0, 5).Select(day => client.CheckIn(Now.AddDays(day))).ToList();
        await InUnitOfWorkAsync(services =>
        {
            services.GetRequiredService<IClientRepository>().Add(client);
            visits.ForEach(services.GetRequiredService<IVisitRepository>().Add);
            return Task.CompletedTask;
        });

        var page = await ReadAsync<IVisitRepository, IReadOnlyList<Visit>>(repository => repository.ListForClientAsync(client.Id, 2, 2, Ct));
        var total = await ReadAsync<IVisitRepository, int>(repository => repository.CountForClientAsync(client.Id, Ct));

        Assert.Equal([visits[2].Id, visits[1].Id], page.Select(visit => visit.Id));
        Assert.Equal(5, total);
    }

    [Fact]
    public async Task ListWithMembershipsEndingBetweenAsync_skips_cancelled_and_out_of_range()
    {
        var expiring = NewClient();
        expiring.PurchaseMembership(await SavedPlanAsync(validityDays: 3), Today, PaymentMethod.Cash, Now);
        var cancelled = NewClient();
        var cancelledPayment = cancelled.PurchaseMembership(await SavedPlanAsync(validityDays: 3), Today, PaymentMethod.Cash, Now);
        cancelled.CancelMembership(cancelledPayment.MembershipId, Now);
        var later = NewClient();
        later.PurchaseMembership(await SavedPlanAsync(validityDays: 60), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients =>
        {
            clients.Add(expiring);
            clients.Add(cancelled);
            clients.Add(later);
        });

        var found = await ReadAsync<IClientRepository, IReadOnlyList<Client>>(
            clients => clients.ListWithMembershipsEndingBetweenAsync(Today, Today.AddDays(3), Ct));

        Assert.Contains(found, c => c.Id == expiring.Id);
        Assert.DoesNotContain(found, c => c.Id == cancelled.Id);
        Assert.DoesNotContain(found, c => c.Id == later.Id);
    }

    [Fact]
    public async Task Payment_round_trips_and_is_listed_by_paid_date()
    {
        var client = NewClient();
        var payment = client.PurchaseMembership(await SavedPlanAsync(), Today, PaymentMethod.Card, Now);
        await InUnitOfWorkAsync(services =>
        {
            services.GetRequiredService<IClientRepository>().Add(client);
            services.GetRequiredService<IPaymentRepository>().Add(payment);
            return Task.CompletedTask;
        });

        var paid = await ReadAsync<IPaymentRepository, IReadOnlyList<Payment>>(
            payments => payments.ListPaidBetweenAsync(Now.AddMinutes(-1), Now.AddMinutes(1), Ct));

        var loaded = Assert.Single(paid, p => p.Id == payment.Id);
        Assert.Equal(Money.Of(800m), loaded.Amount);
        Assert.Equal(PaymentMethod.Card, loaded.Method);
    }

    [Fact]
    public async Task Concurrent_changes_to_the_same_client_raise_conflict()
    {
        var client = NewClient();
        client.PurchaseMembership(await SavedPlanAsync(visitLimit: 1), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        await using var first = Factory.Services.CreateAsyncScope();
        await using var second = Factory.Services.CreateAsyncScope();
        var firstCopy = await first.ServiceProvider.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
        var secondCopy = await second.ServiceProvider.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
        first.ServiceProvider.GetRequiredService<IVisitRepository>().Add(firstCopy!.CheckIn(Now));
        second.ServiceProvider.GetRequiredService<IVisitRepository>().Add(secondCopy!.CheckIn(Now));

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        await Assert.ThrowsAsync<ConflictException>(() => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
        var reloaded = await ReadAsync<IClientRepository, Client?>(clients => clients.GetByIdAsync(client.Id, Ct));
        var history = await ReadAsync<IVisitRepository, IReadOnlyList<Visit>>(visits => visits.ListForClientAsync(client.Id, 0, 10, Ct));
        Assert.Equal(0, reloaded!.Memberships.Single().RemainingVisits);
        Assert.Single(history);
    }

    [Fact]
    public async Task Purchase_rejected_after_a_concurrent_check_in_stores_no_membership_or_payment()
    {
        var client = NewClient();
        client.PurchaseMembership(await SavedPlanAsync(), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        await using var checkIn = Factory.Services.CreateAsyncScope();
        await using var purchase = Factory.Services.CreateAsyncScope();
        var checkInCopy = await checkIn.ServiceProvider.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
        var purchaseCopy = await purchase.ServiceProvider.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
        checkIn.ServiceProvider.GetRequiredService<IVisitRepository>().Add(checkInCopy!.CheckIn(Now));
        var payment = purchaseCopy!.PurchaseMembership(await SavedPlanAsync(), Today.AddDays(30), PaymentMethod.Card, Now);
        purchase.ServiceProvider.GetRequiredService<IPaymentRepository>().Add(payment);

        await checkIn.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        await Assert.ThrowsAsync<ConflictException>(() => purchase.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
        var reloaded = await ReadAsync<IClientRepository, Client?>(clients => clients.GetByIdAsync(client.Id, Ct));
        Assert.Single(reloaded!.Memberships);
        Assert.Null(await ReadAsync<IPaymentRepository, Payment?>(payments => payments.GetByIdAsync(payment.Id, Ct)));
    }

    [Fact]
    public async Task Changing_only_a_child_entity_still_bumps_the_aggregate_version()
    {
        var client = NewClient();
        client.PurchaseMembership(await SavedPlanAsync(), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        await using var stale = Factory.Services.CreateAsyncScope();
        var staleCopy = await stale.ServiceProvider.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
        await ChangeAsync<IClientRepository>(async clients =>
            (await clients.GetByIdAsync(client.Id, Ct))!.CancelMembership(client.Memberships.Single().Id, Now));

        staleCopy!.UpdateProfile(staleCopy.Name, staleCopy.DateOfBirth, staleCopy.Email, PhoneNumber.Create(UniquePhone()), Now);

        await Assert.ThrowsAsync<ConflictException>(() => stale.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
    }
}
