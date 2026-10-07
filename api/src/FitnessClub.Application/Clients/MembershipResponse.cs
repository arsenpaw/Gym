using FitnessClub.Domain.Clients;

namespace FitnessClub.Application.Clients;

public sealed record MembershipResponse(
    Guid Id,
    Guid PlanId,
    string PlanName,
    decimal Price,
    DateOnly StartsOn,
    DateOnly EndsOn,
    int? VisitLimit,
    int VisitsUsed,
    int? VisitsLeft,
    DateTimeOffset PurchasedAt,
    DateTimeOffset? CancelledAt,
    bool IsActive)
{
    public static MembershipResponse FromEntity(Membership membership, DateOnly today) =>
        new(
            membership.Id,
            membership.PlanId,
            membership.PlanName,
            membership.Price.Amount,
            membership.StartsOn,
            membership.EndsOn,
            membership.VisitLimit,
            membership.VisitsUsed,
            membership.RemainingVisits,
            membership.PurchasedAt,
            membership.CancelledAt,
            membership.IsActiveOn(today));
}
