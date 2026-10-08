using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.UnitTests.Domain;

public class ClientTests
{
    private static readonly PersonName Name = PersonName.Create("Olena", "Shevchenko", null);
    private static readonly PhoneNumber Phone = PhoneNumber.Create("+380671234567");

    [Fact]
    public void Register_sets_profile_and_registration_time()
    {
        var client = Client.Register(Name, new DateOnly(1995, 3, 14), Phone, null, TestData.Now);

        Assert.Equal(Name, client.Name);
        Assert.Equal(Phone, client.Phone);
        Assert.Equal(TestData.Now, client.RegisteredAt);
        Assert.Empty(client.Memberships);
    }

    [Fact]
    public void Register_with_future_date_of_birth_throws()
    {
        Assert.Throws<DomainException>(() => Client.Register(Name, TestData.Today.AddDays(1), Phone, null, TestData.Now));
    }

    [Fact]
    public void Register_older_than_max_age_throws()
    {
        var dateOfBirth = TestData.Today.AddYears(-Client.MaxAge - 1);

        Assert.Throws<DomainException>(() => Client.Register(Name, dateOfBirth, Phone, null, TestData.Now));
    }

    [Theory]
    [InlineData(4, 31)]
    [InlineData(5, 31)]
    [InlineData(6, 30)]
    public void AgeOn_counts_full_years(int birthDay, int expectedAge)
    {
        var client = Client.Register(Name, new DateOnly(1995, 10, birthDay), Phone, null, TestData.Now);

        Assert.Equal(expectedAge, client.AgeOn(TestData.Today));
    }

    [Fact]
    public void PurchaseMembership_snapshots_plan_and_computes_end_date()
    {
        var client = TestData.Client();
        var plan = TestData.Plan(validityDays: 30, visitLimit: 12, price: 950m);

        var membership = TestData.Buy(client, plan);

        Assert.Equal(plan.Id, membership.PlanId);
        Assert.Equal(plan.Name, membership.PlanName);
        Assert.Equal(Money.Of(950m), membership.Price);
        Assert.Equal(TestData.Today, membership.StartsOn);
        Assert.Equal(TestData.Today.AddDays(29), membership.EndsOn);
        Assert.Equal(12, membership.RemainingVisits);
        Assert.Contains(membership, client.Memberships);
    }

    [Fact]
    public void PurchaseMembership_of_inactive_plan_throws()
    {
        var plan = TestData.Plan();
        plan.Deactivate();

        Assert.Throws<DomainException>(() => TestData.Buy(TestData.Client(), plan));
    }

    [Fact]
    public void PurchaseMembership_starting_in_the_past_throws()
    {
        Assert.Throws<DomainException>(() => TestData.Buy(TestData.Client(), TestData.Plan(), TestData.Today.AddDays(-1)));
    }

    [Fact]
    public void PurchaseMembership_overlapping_a_live_membership_throws()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);

        Assert.Throws<DomainException>(() => TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(29)));
    }

    [Fact]
    public void PurchaseMembership_right_after_the_current_one_ends_is_allowed()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);

        var next = TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(30));

        Assert.Equal(2, client.Memberships.Count);
        Assert.Equal(TestData.Today.AddDays(30), next.StartsOn);
    }

    [Fact]
    public void PurchaseMembership_after_single_visit_is_used_up_is_allowed_the_same_day()
    {
        var client = TestData.ClientWithMembership(validityDays: 1, visitLimit: 1);
        client.CheckIn(TestData.Now);

        TestData.Buy(client, TestData.Plan(validityDays: 1, visitLimit: 1));

        Assert.True(client.HasActiveMembershipOn(TestData.Today));
    }

    [Fact]
    public void CheckIn_again_the_same_day_on_a_newly_bought_membership_is_allowed()
    {
        var client = TestData.ClientWithMembership(validityDays: 1, visitLimit: 1);
        var first = client.CheckIn(TestData.Now);
        var second = TestData.Buy(client, TestData.Plan(validityDays: 1, visitLimit: 1));

        var visit = client.CheckIn(TestData.Now.AddHours(2));

        Assert.Equal(second.Id, visit.MembershipId);
        Assert.NotEqual(first.MembershipId, visit.MembershipId);
        Assert.Equal(0, second.RemainingVisits);
    }

    [Fact]
    public void PurchaseMembership_returns_payment_for_the_new_membership()
    {
        var client = TestData.Client();

        var payment = client.PurchaseMembership(TestData.Plan(price: 1200m), TestData.Today, PaymentMethod.Card, TestData.Now);

        var membership = Assert.Single(client.Memberships);
        Assert.Equal(client.Id, payment.ClientId);
        Assert.Equal(membership.Id, payment.MembershipId);
        Assert.Equal(Money.Of(1200m), payment.Amount);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Equal(TestData.Now, payment.PaidAt);
    }

    [Fact]
    public void PurchaseMembership_with_unknown_payment_method_throws_and_adds_nothing()
    {
        var client = TestData.Client();

        Assert.Throws<DomainException>(() => client.PurchaseMembership(TestData.Plan(), TestData.Today, (PaymentMethod)42, TestData.Now));
        Assert.Empty(client.Memberships);
    }

    [Fact]
    public void CheckIn_records_visit_and_uses_one_visit()
    {
        var client = TestData.ClientWithMembership(visitLimit: 10);
        var membership = client.Memberships.Single();

        var visit = client.CheckIn(TestData.Now);

        Assert.Equal(client.Id, visit.ClientId);
        Assert.Equal(membership.Id, visit.MembershipId);
        Assert.Equal(TestData.Now, visit.CheckedInAt);
        Assert.Equal(9, membership.RemainingVisits);
    }

    [Fact]
    public void CheckIn_without_membership_throws()
    {
        Assert.Throws<DomainException>(() => TestData.Client().CheckIn(TestData.Now));
    }

    [Fact]
    public void CheckIn_after_membership_expired_throws()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);

        Assert.Throws<DomainException>(() => client.CheckIn(TestData.Now.AddDays(30)));
    }

    [Fact]
    public void CheckIn_when_visits_are_used_up_throws()
    {
        var client = TestData.ClientWithMembership(visitLimit: 1);
        client.CheckIn(TestData.Now);

        Assert.Throws<DomainException>(() => client.CheckIn(TestData.Now.AddDays(1)));
    }

    [Fact]
    public void CheckIn_twice_on_the_same_day_throws_and_uses_one_visit()
    {
        var client = TestData.ClientWithMembership(visitLimit: 10);
        client.CheckIn(TestData.Now);

        Assert.Throws<DomainException>(() => client.CheckIn(TestData.Now.AddHours(3)));
        Assert.Equal(9, client.Memberships.Single().RemainingVisits);
    }

    [Fact]
    public void CheckIn_on_the_next_day_uses_another_visit()
    {
        var client = TestData.ClientWithMembership(visitLimit: 10);
        client.CheckIn(TestData.Now);

        client.CheckIn(TestData.Now.AddDays(1));

        Assert.Equal(8, client.Memberships.Single().RemainingVisits);
    }

    [Fact]
    public void NeedsExpiryNotice_is_false_once_the_client_has_renewed()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);
        var current = client.Memberships.Single();
        Assert.True(client.NeedsExpiryNotice(current, TestData.Today));

        TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(30));

        Assert.False(client.NeedsExpiryNotice(current, TestData.Today));
    }

    [Fact]
    public void NeedsExpiryNotice_is_false_for_ended_or_used_up_memberships()
    {
        var ended = TestData.ClientWithMembership(validityDays: 30);
        var usedUp = TestData.ClientWithMembership(visitLimit: 1);
        usedUp.CheckIn(TestData.Now);

        Assert.False(ended.NeedsExpiryNotice(ended.Memberships.Single(), TestData.Today.AddDays(30)));
        Assert.False(usedUp.NeedsExpiryNotice(usedUp.Memberships.Single(), TestData.Today));
    }

    [Fact]
    public void NeedsExpiryNotice_is_false_for_a_client_without_email()
    {
        var client = TestData.ClientWithMembership(email: null);

        Assert.False(client.NeedsExpiryNotice(client.Memberships.Single(), TestData.Today));
    }

    [Fact]
    public void MembershipsNeedingExpiryNotice_returns_only_those_ending_by_the_date()
    {
        var client = TestData.ClientWithMembership(validityDays: 3);

        Assert.Single(client.MembershipsNeedingExpiryNotice(TestData.Today, TestData.Today.AddDays(3)));
        Assert.Empty(client.MembershipsNeedingExpiryNotice(TestData.Today, TestData.Today.AddDays(1)));
    }

    [Fact]
    public void CancelMembership_makes_it_inactive()
    {
        var client = TestData.ClientWithMembership();
        var membership = client.Memberships.Single();

        client.CancelMembership(membership.Id, TestData.Now);

        Assert.True(membership.IsCancelled);
        Assert.False(client.HasActiveMembershipOn(TestData.Today));
    }

    [Fact]
    public void CancelMembership_twice_throws()
    {
        var client = TestData.ClientWithMembership();
        var membershipId = client.Memberships.Single().Id;
        client.CancelMembership(membershipId, TestData.Now);

        Assert.Throws<DomainException>(() => client.CancelMembership(membershipId, TestData.Now));
    }

    [Fact]
    public void CancelMembership_of_another_client_throws()
    {
        Assert.Throws<DomainException>(() => TestData.ClientWithMembership().CancelMembership(Guid.NewGuid(), TestData.Now));
    }
}
