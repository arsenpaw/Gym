using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;

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
            PhoneNumber.Create("+380671234567"),
            email is null ? null : EmailAddress.Create(email),
            Now);

    public static Client ClientWithMembership(int validityDays = 30, int? visitLimit = null)
    {
        var client = Client();
        Buy(client, Plan(validityDays, visitLimit));
        return client;
    }

    public static Membership Buy(Client client, MembershipPlan plan, DateOnly? startsOn = null)
    {
        var payment = client.PurchaseMembership(plan, startsOn ?? Today, PaymentMethod.Cash, Now);
        return client.Memberships.Single(m => m.Id == payment.MembershipId);
    }
}
