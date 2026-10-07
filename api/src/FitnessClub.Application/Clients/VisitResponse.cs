using FitnessClub.Domain.Visits;

namespace FitnessClub.Application.Clients;

public sealed record VisitResponse(Guid Id, Guid ClientId, Guid MembershipId, DateTimeOffset CheckedInAt)
{
    public static VisitResponse FromEntity(Visit visit) => new(visit.Id, visit.ClientId, visit.MembershipId, visit.CheckedInAt);
}
