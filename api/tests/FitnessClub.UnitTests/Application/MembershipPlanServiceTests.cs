using FitnessClub.Application.Common;
using FitnessClub.Application.MembershipPlans;
using FitnessClub.Domain.Common;
using FitnessClub.UnitTests.Fakes;

namespace FitnessClub.UnitTests.Application;

public class MembershipPlanServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MembershipPlanService _service;

    public MembershipPlanServiceTests()
    {
        _service = new MembershipPlanService(new InMemoryMembershipPlanRepository(), _unitOfWork);
    }

    private static MembershipPlanRequest Request(string name = "Monthly", decimal price = 800m, int validityDays = 30, int? visitLimit = null) =>
        new() { Name = name, Price = price, ValidityDays = validityDays, VisitLimit = visitLimit };

    [Fact]
    public async Task CreateAsync_returns_saved_plan()
    {
        var created = await _service.CreateAsync(Request(), Ct);

        var loaded = await _service.GetAsync(created.Id, Ct);
        Assert.Equal(created, loaded);
        Assert.True(loaded.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_with_duplicate_name_ignoring_case_and_spaces_throws_conflict_and_does_not_save()
    {
        await _service.CreateAsync(Request("Monthly"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(Request("  MONTHLY "), Ct));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_with_invalid_domain_values_throws_domain_exception()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(Request(price: 10.001m), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetAsync_for_missing_plan_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task UpdateAsync_keeping_own_name_succeeds()
    {
        var created = await _service.CreateAsync(Request("Monthly", 800m), Ct);

        var updated = await _service.UpdateAsync(created.Id, Request("Monthly", 900m), Ct);

        Assert.Equal(900m, updated.Price);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_to_another_plans_name_throws_conflict()
    {
        await _service.CreateAsync(Request("Monthly"), Ct);
        var yearly = await _service.CreateAsync(Request("Yearly", 8000m, 365), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(yearly.Id, Request("monthly"), Ct));

        Assert.Equal("Yearly", (await _service.GetAsync(yearly.Id, Ct)).Name);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_for_missing_plan_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(Guid.NewGuid(), Request(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeactivateAsync_for_missing_plan_throws_not_found_and_does_not_save()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeactivateAsync(Guid.NewGuid(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ListAsync_hides_inactive_plans_unless_requested()
    {
        var monthly = await _service.CreateAsync(Request("Monthly"), Ct);
        await _service.CreateAsync(Request("Yearly", 8000m, 365), Ct);
        await _service.DeactivateAsync(monthly.Id, Ct);

        var activeOnly = await _service.ListAsync(includeInactive: false, Ct);
        var all = await _service.ListAsync(includeInactive: true, Ct);

        Assert.Equal(["Yearly"], activeOnly.Select(p => p.Name));
        Assert.Equal(["Monthly", "Yearly"], all.Select(p => p.Name));
    }

    [Fact]
    public async Task ActivateAsync_reactivates_plan()
    {
        var plan = await _service.CreateAsync(Request(), Ct);
        await _service.DeactivateAsync(plan.Id, Ct);

        await _service.ActivateAsync(plan.Id, Ct);

        Assert.True((await _service.GetAsync(plan.Id, Ct)).IsActive);
    }
}
