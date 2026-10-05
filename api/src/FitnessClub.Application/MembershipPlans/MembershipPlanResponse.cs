using FitnessClub.Domain.MembershipPlans;

namespace FitnessClub.Application.MembershipPlans;

public sealed record MembershipPlanResponse(
    Guid Id,
    string Name,
    decimal Price,
    int ValidityDays,
    int? VisitLimit,
    bool IsActive)
{
    public static MembershipPlanResponse FromEntity(MembershipPlan plan) =>
        new(plan.Id, plan.Name, plan.Price.Amount, plan.ValidityDays, plan.VisitLimit, plan.IsActive);
}
