using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.UnitTests.Domain;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 5);

    public static MembershipPlan Plan(int validityDays = 30, int? visitLimit = null, decimal price = 800m) =>
        MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(price), validityDays, visitLimit);

    public static Client Client(string? email = null) =>
        FitnessClub.Domain.Clients.Client.Register(
            PersonName.Create("Olena", "Shevchenko", null),
            new DateOnly(1995, 3, 14),
            PhoneNumber.Create(UniquePhone()),
            email is null ? null : EmailAddress.Create(email),
            Now);

    public static Client ClientWithMembership(int validityDays = 30, int? visitLimit = null, string? email = "olena@example.com")
    {
        var client = Client(email);
        Buy(client, Plan(validityDays, visitLimit));
        return client;
    }

    public static Membership Buy(Client client, MembershipPlan plan, DateOnly? startsOn = null)
    {
        var payment = client.PurchaseMembership(plan, startsOn ?? Today, PaymentMethod.Cash, Now);
        return client.Memberships.Single(m => m.Id == payment.MembershipId);
    }

    public static Room Room(int capacity = 20) => FitnessClub.Domain.Rooms.Room.Create($"Room {Guid.NewGuid():N}", capacity);

    public static Trainer Trainer()
    {
        var trainer = FitnessClub.Domain.Trainers.Trainer.Hire(
            PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create(UniquePhone()), null, "Yoga");
        trainer.SetWorkingHours(Enum.GetValues<DayOfWeek>().Select(day => WorkingHours.Create(day, new TimeOnly(8, 0), new TimeOnly(20, 0))));
        return trainer;
    }

    public static string UniquePhone() => $"+380{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

    public static TimeSlot Slot(int startHour = 10, int durationMinutes = 60, int daysFromToday = 1)
    {
        var start = new DateTimeOffset(Today.AddDays(daysFromToday), new TimeOnly(startHour, 0), TimeSpan.Zero);
        return TimeSlot.Create(start, start.AddMinutes(durationMinutes));
    }
}
