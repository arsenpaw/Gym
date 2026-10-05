using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Visits;

public sealed class Visit : AggregateRoot
{
    public Guid ClientId { get; private set; }
    public Guid MembershipId { get; private set; }
    public DateTimeOffset CheckedInAt { get; private set; }

    private Visit()
    {
    }

    internal static Visit Record(Guid clientId, Guid membershipId, DateTimeOffset checkedInAt) =>
        new() { ClientId = clientId, MembershipId = membershipId, CheckedInAt = checkedInAt };
}
