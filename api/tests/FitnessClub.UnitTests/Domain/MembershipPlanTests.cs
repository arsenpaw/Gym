using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.UnitTests.Domain;

public class MembershipPlanTests
{
    private static readonly Money Price = Money.Of(800m);

    [Fact]
    public void Create_with_valid_values_sets_fields_and_is_active()
    {
        var plan = MembershipPlan.Create("  Monthly  ", Price, 30, null);

        Assert.NotEqual(Guid.Empty, plan.Id);
        Assert.Equal("Monthly", plan.Name);
        Assert.Equal(Price, plan.Price);
        Assert.Equal(30, plan.ValidityDays);
        Assert.Null(plan.VisitLimit);
        Assert.True(plan.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_blank_name_throws(string name)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create(name, Price, 30, null));
    }

    [Fact]
    public void Create_with_name_longer_than_max_throws()
    {
        var name = new string('a', MembershipPlan.NameMaxLength + 1);

        Assert.Throws<DomainException>(() => MembershipPlan.Create(name, Price, 30, null));
    }

    [Fact]
    public void Create_with_zero_price_throws()
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", Money.Of(0m), 30, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MembershipPlan.MaxValidityDays + 1)]
    public void Create_with_validity_out_of_range_throws(int validityDays)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", Price, validityDays, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MembershipPlan.MaxVisitLimit + 1)]
    public void Create_with_visit_limit_out_of_range_throws(int visitLimit)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", Price, 30, visitLimit));
    }

    [Fact]
    public void Update_with_invalid_values_leaves_plan_unchanged()
    {
        var plan = MembershipPlan.Create("Monthly", Price, 30, null);

        Assert.Throws<DomainException>(() => plan.Update("Yearly", Price, 0, null));

        Assert.Equal("Monthly", plan.Name);
        Assert.Equal(30, plan.ValidityDays);
    }

    [Fact]
    public void Deactivate_then_activate_toggles_IsActive()
    {
        var plan = MembershipPlan.Create("Monthly", Price, 30, null);

        plan.Deactivate();
        Assert.False(plan.IsActive);

        plan.Activate();
        Assert.True(plan.IsActive);
    }
}
