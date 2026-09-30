using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;

namespace FitnessClub.UnitTests.Domain;

public class MembershipPlanTests
{
    [Fact]
    public void Create_with_valid_values_sets_fields_and_is_active()
    {
        var plan = MembershipPlan.Create("  Monthly  ", 800m, 30, null);

        Assert.NotEqual(Guid.Empty, plan.Id);
        Assert.Equal("Monthly", plan.Name);
        Assert.Equal(800m, plan.Price);
        Assert.Equal(30, plan.ValidityDays);
        Assert.Null(plan.VisitLimit);
        Assert.True(plan.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_blank_name_throws(string name)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create(name, 100m, 30, null));
    }

    [Fact]
    public void Create_with_name_longer_than_max_throws()
    {
        var name = new string('a', MembershipPlan.NameMaxLength + 1);

        Assert.Throws<DomainException>(() => MembershipPlan.Create(name, 100m, 30, null));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("10.001")]
    public void Create_with_invalid_price_throws(string price)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), 30, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MembershipPlan.MaxValidityDays + 1)]
    public void Create_with_validity_out_of_range_throws(int validityDays)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", 100m, validityDays, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MembershipPlan.MaxVisitLimit + 1)]
    public void Create_with_visit_limit_out_of_range_throws(int visitLimit)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", 100m, 30, visitLimit));
    }

    [Fact]
    public void Update_with_invalid_values_leaves_plan_unchanged()
    {
        var plan = MembershipPlan.Create("Monthly", 800m, 30, null);

        Assert.Throws<DomainException>(() => plan.Update("Yearly", -1m, 365, null));

        Assert.Equal("Monthly", plan.Name);
        Assert.Equal(800m, plan.Price);
    }

    [Fact]
    public void Deactivate_then_activate_toggles_IsActive()
    {
        var plan = MembershipPlan.Create("Monthly", 800m, 30, null);

        plan.Deactivate();
        Assert.False(plan.IsActive);

        plan.Activate();
        Assert.True(plan.IsActive);
    }
}
