using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Sessions;

internal sealed class SessionSeeder(FitnessClubApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DateTimeOffset Now => factory.Services.GetRequiredService<TimeProvider>().GetUtcNow();

    public DateTimeOffset SlotStart(int daysAhead = 2, int hour = 10) =>
        new(Now.UtcDateTime.Date.AddDays(daysAhead).AddHours(hour), TimeSpan.Zero);

    public async Task<Trainer> TrainerAsync(string? identityUserId = null)
    {
        var trainer = Trainer.Hire(PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create(UniquePhone()), null, "Yoga");
        trainer.SetWorkingHours(Enum.GetValues<DayOfWeek>().Select(day => WorkingHours.Create(day, new TimeOnly(6, 0), new TimeOnly(22, 0))));
        if (identityUserId is not null)
            trainer.LinkIdentity(identityUserId);

        await SaveAsync<ITrainerRepository>(trainers => trainers.Add(trainer));
        return trainer;
    }

    public async Task<Room> RoomAsync(int capacity = 20)
    {
        var room = Room.Create($"Room {Guid.NewGuid():N}", capacity);
        await SaveAsync<IRoomRepository>(rooms => rooms.Add(room));
        return room;
    }

    public async Task<Client> ClientAsync(bool withMembership = true)
    {
        var now = Now;
        var client = Client.Register(
            PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), null, now);
        if (withMembership)
        {
            var plan = MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(800m), 30, null);
            await SaveAsync<IMembershipPlanRepository>(plans => plans.Add(plan));
            client.PurchaseMembership(plan, DateOnly.FromDateTime(now.UtcDateTime), PaymentMethod.Cash, now);
        }

        await SaveAsync<IClientRepository>(clients => clients.Add(client));
        return client;
    }

    private async Task SaveAsync<TRepository>(Action<TRepository> change) where TRepository : notnull
    {
        await using var scope = factory.Services.CreateAsyncScope();
        change(scope.ServiceProvider.GetRequiredService<TRepository>());
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
    }

    private static string UniquePhone() => $"+380{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
}
