using FitnessClub.Domain.Clients;

namespace FitnessClub.Application.Clients;

public sealed record ActiveMembershipResponse(Guid Id, string PlanName, DateOnly StartsOn, DateOnly EndsOn, int? VisitsLeft)
{
    public static ActiveMembershipResponse? For(Client client, DateOnly today) =>
        client.ActiveMembershipOn(today) is { } membership
            ? new(membership.Id, membership.PlanName, membership.StartsOn, membership.EndsOn, membership.RemainingVisits)
            : null;
}
