using FitnessClub.Application.Clients;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.UnitTests.Fakes;

namespace FitnessClub.UnitTests.Application;

public class ClientServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly InMemoryMembershipPlanRepository _plans = new();
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly InMemoryVisitRepository _visits = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly ClientService _service;

    public ClientServiceTests()
    {
        _service = new ClientService(new InMemoryClientRepository(), _plans, _payments, _visits, _unitOfWork, _time);
    }

    private static ClientRequest Request(
        string firstName = "Olena",
        string lastName = "Shevchenko",
        string phone = "+380671234567",
        string? email = null,
        DateOnly? dateOfBirth = null) =>
        new()
        {
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dateOfBirth ?? new DateOnly(1995, 3, 14),
            Phone = phone,
            Email = email,
        };

    private MembershipPlan AddPlan(int validityDays = 30, int? visitLimit = null, decimal price = 800m)
    {
        var plan = MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(price), validityDays, visitLimit);
        _plans.Add(plan);
        return plan;
    }

    private static PurchaseMembershipRequest Purchase(Guid planId, DateOnly? startsOn = null, PaymentMethod method = PaymentMethod.Card) =>
        new() { PlanId = planId, StartsOn = startsOn, PaymentMethod = method };

    [Fact]
    public async Task RegisterAsync_returns_saved_client_with_age_today()
    {
        var created = await _service.RegisterAsync(Request(email: "Olena@Example.com"), Ct);

        var loaded = await _service.GetAsync(created.Id, Ct);
        Assert.Equal("Shevchenko Olena", loaded.FullName);
        Assert.Equal(31, loaded.Age);
        Assert.Equal("olena@example.com", loaded.Email);
        Assert.Equal(Now, loaded.RegisteredAt);
        Assert.Null(loaded.ActiveMembership);
        Assert.Empty(loaded.Memberships);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RegisterAsync_with_blank_email_stores_no_email()
    {
        var created = await _service.RegisterAsync(Request(email: "  "), Ct);

        Assert.Null(created.Email);
    }

    [Fact]
    public async Task RegisterAsync_with_same_phone_in_another_format_throws_conflict_and_does_not_save()
    {
        await _service.RegisterAsync(Request(phone: "+380671234567"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.RegisterAsync(Request(phone: "+38 (067) 123-45-67"), Ct));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RegisterAsync_with_invalid_phone_throws_domain_exception()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.RegisterAsync(Request(phone: "12345"), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RegisterAsync_with_future_birth_date_throws_domain_exception()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.RegisterAsync(Request(dateOfBirth: Today.AddDays(1)), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetAsync_for_missing_client_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task UpdateAsync_keeping_own_phone_succeeds()
    {
        var created = await _service.RegisterAsync(Request(), Ct);

        var updated = await _service.UpdateAsync(created.Id, Request(firstName: "Oksana", email: "oksana@example.com"), Ct);

        Assert.Equal("Shevchenko Oksana", updated.FullName);
        Assert.Equal("oksana@example.com", updated.Email);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_to_another_clients_phone_throws_conflict()
    {
        await _service.RegisterAsync(Request(phone: "+380671234567"), Ct);
        var other = await _service.RegisterAsync(Request(phone: "+380501112233"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(other.Id, Request(phone: "+380671234567"), Ct));

        Assert.Equal("+380501112233", (await _service.GetAsync(other.Id, Ct)).Phone);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_for_missing_client_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(Guid.NewGuid(), Request(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task PurchaseMembershipAsync_starts_today_by_default_and_records_payment_in_same_save()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        var plan = AddPlan(validityDays: 30, visitLimit: 12, price: 950m);

        var membership = await _service.PurchaseMembershipAsync(client.Id, Purchase(plan.Id), Ct);

        Assert.Equal(plan.Name, membership.PlanName);
        Assert.Equal(950m, membership.Price);
        Assert.Equal(Today, membership.StartsOn);
        Assert.Equal(Today.AddDays(29), membership.EndsOn);
        Assert.Equal(12, membership.VisitsLeft);
        Assert.True(membership.IsActive);
        var payment = Assert.Single(_payments.All);
        Assert.Equal(membership.Id, payment.MembershipId);
        Assert.Equal(client.Id, payment.ClientId);
        Assert.Equal(950m, payment.Amount.Amount);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task PurchaseMembershipAsync_in_advance_is_listed_but_not_active_yet()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        var plan = AddPlan();

        var membership = await _service.PurchaseMembershipAsync(client.Id, Purchase(plan.Id, Today.AddDays(10)), Ct);

        Assert.False(membership.IsActive);
        var details = await _service.GetAsync(client.Id, Ct);
        Assert.Null(details.ActiveMembership);
        Assert.Equal(membership.Id, Assert.Single(details.Memberships).Id);
    }

    [Fact]
    public async Task PurchaseMembershipAsync_for_missing_plan_throws_not_found_and_records_nothing()
    {
        var client = await _service.RegisterAsync(Request(), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.PurchaseMembershipAsync(client.Id, Purchase(Guid.NewGuid()), Ct));

        Assert.Empty(_payments.All);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task PurchaseMembershipAsync_for_missing_client_throws_not_found()
    {
        var plan = AddPlan();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.PurchaseMembershipAsync(Guid.NewGuid(), Purchase(plan.Id), Ct));
    }

    [Fact]
    public async Task PurchaseMembershipAsync_for_inactive_plan_throws_domain_exception()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        var plan = AddPlan();
        plan.Deactivate();

        await Assert.ThrowsAsync<DomainException>(() => _service.PurchaseMembershipAsync(client.Id, Purchase(plan.Id), Ct));
        Assert.Empty(_payments.All);
    }

    [Fact]
    public async Task PurchaseMembershipAsync_overlapping_existing_membership_throws_domain_exception()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        var plan = AddPlan();
        await _service.PurchaseMembershipAsync(client.Id, Purchase(plan.Id), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.PurchaseMembershipAsync(client.Id, Purchase(plan.Id, Today.AddDays(5)), Ct));
        Assert.Single(_payments.All);
    }

    [Fact]
    public async Task CancelMembershipAsync_makes_membership_inactive()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        var membership = await _service.PurchaseMembershipAsync(client.Id, Purchase(AddPlan().Id), Ct);

        await _service.CancelMembershipAsync(client.Id, membership.Id, Ct);

        var details = await _service.GetAsync(client.Id, Ct);
        Assert.Null(details.ActiveMembership);
        var cancelled = Assert.Single(details.Memberships);
        Assert.Equal(Now, cancelled.CancelledAt);
        Assert.False(cancelled.IsActive);
        Assert.Equal(3, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelMembershipAsync_twice_throws_domain_exception()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        var membership = await _service.PurchaseMembershipAsync(client.Id, Purchase(AddPlan().Id), Ct);
        await _service.CancelMembershipAsync(client.Id, membership.Id, Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.CancelMembershipAsync(client.Id, membership.Id, Ct));
    }

    [Fact]
    public async Task CancelMembershipAsync_for_unknown_membership_throws_not_found()
    {
        var client = await _service.RegisterAsync(Request(), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CancelMembershipAsync(client.Id, Guid.NewGuid(), Ct));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CheckInAsync_records_visit_and_uses_one_visit()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        var membership = await _service.PurchaseMembershipAsync(client.Id, Purchase(AddPlan(visitLimit: 8).Id), Ct);

        var visit = await _service.CheckInAsync(client.Id, Ct);

        Assert.Equal(client.Id, visit.ClientId);
        Assert.Equal(membership.Id, visit.MembershipId);
        Assert.Equal(Now, visit.CheckedInAt);
        Assert.Single(_visits.All);
        Assert.Equal(7, (await _service.GetAsync(client.Id, Ct)).ActiveMembership?.VisitsLeft);
        Assert.Equal(3, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CheckInAsync_without_active_membership_throws_domain_exception_and_records_nothing()
    {
        var client = await _service.RegisterAsync(Request(), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.CheckInAsync(client.Id, Ct));
        Assert.Empty(_visits.All);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CheckInAsync_twice_on_the_same_day_throws_domain_exception()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        await _service.PurchaseMembershipAsync(client.Id, Purchase(AddPlan().Id), Ct);
        await _service.CheckInAsync(client.Id, Ct);
        _time.Advance(TimeSpan.FromHours(3));

        await Assert.ThrowsAsync<DomainException>(() => _service.CheckInAsync(client.Id, Ct));
        Assert.Single(_visits.All);
    }

    [Fact]
    public async Task CheckInAsync_for_missing_client_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CheckInAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ListVisitsAsync_returns_newest_first()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        await _service.PurchaseMembershipAsync(client.Id, Purchase(AddPlan().Id), Ct);
        var first = await _service.CheckInAsync(client.Id, Ct);
        _time.Advance(TimeSpan.FromDays(1));
        var second = await _service.CheckInAsync(client.Id, Ct);

        var history = await _service.ListVisitsAsync(client.Id, Ct);

        Assert.Equal([second.Id, first.Id], history.Select(v => v.Id));
    }

    [Fact]
    public async Task ListVisitsAsync_for_missing_client_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListVisitsAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ListAsync_shows_active_membership_summary_or_null()
    {
        var withMembership = await _service.RegisterAsync(Request(lastName: "Antonenko", phone: "+380671111111"), Ct);
        await _service.RegisterAsync(Request(lastName: "Bondarenko", phone: "+380672222222"), Ct);
        var plan = AddPlan(validityDays: 30, visitLimit: 10);
        await _service.PurchaseMembershipAsync(withMembership.Id, Purchase(plan.Id), Ct);

        var list = await _service.ListAsync(Ct);

        Assert.Equal(["Antonenko Olena", "Bondarenko Olena"], list.Select(c => c.FullName));
        var active = list[0].ActiveMembership;
        Assert.NotNull(active);
        Assert.Equal(plan.Name, active.PlanName);
        Assert.Equal(Today.AddDays(29), active.EndsOn);
        Assert.Equal(10, active.VisitsLeft);
        Assert.Equal(31, list[0].Age);
        Assert.Null(list[1].ActiveMembership);
    }

    [Fact]
    public async Task ListAsync_drops_membership_after_it_ends()
    {
        var client = await _service.RegisterAsync(Request(), Ct);
        await _service.PurchaseMembershipAsync(client.Id, Purchase(AddPlan(validityDays: 1).Id), Ct);
        _time.Advance(TimeSpan.FromDays(1));

        var list = await _service.ListAsync(Ct);

        Assert.Null(Assert.Single(list).ActiveMembership);
    }
}
