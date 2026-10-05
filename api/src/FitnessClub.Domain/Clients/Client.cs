using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Visits;

namespace FitnessClub.Domain.Clients;

public sealed class Client : AggregateRoot
{
    public const int MaxAge = 120;

    private readonly List<Membership> _memberships = [];

    public PersonName Name { get; private set; } = null!;
    public DateOnly DateOfBirth { get; private set; }
    public PhoneNumber Phone { get; private set; } = null!;
    public EmailAddress? Email { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public IReadOnlyCollection<Membership> Memberships => _memberships.AsReadOnly();

    private Client()
    {
    }

    public static Client Register(PersonName name, DateOnly dateOfBirth, PhoneNumber phone, EmailAddress? email, DateTimeOffset now)
    {
        var client = new Client { RegisteredAt = now };
        client.UpdateProfile(name, dateOfBirth, phone, email, now);
        return client;
    }

    public void UpdateProfile(PersonName name, DateOnly dateOfBirth, PhoneNumber phone, EmailAddress? email, DateTimeOffset now)
    {
        var today = now.ToDateOnly();
        if (dateOfBirth > today)
            throw new DomainException("Date of birth cannot be in the future.");

        if (AgeOn(dateOfBirth, today) > MaxAge)
            throw new DomainException($"Age cannot be more than {MaxAge} years.");

        Name = name;
        DateOfBirth = dateOfBirth;
        Phone = phone;
        Email = email;
    }

    public int AgeOn(DateOnly date) => AgeOn(DateOfBirth, date);

    public Membership? ActiveMembershipOn(DateOnly date) =>
        _memberships.Where(m => m.IsActiveOn(date)).OrderBy(m => m.EndsOn).FirstOrDefault();

    public bool HasActiveMembershipOn(DateOnly date) => ActiveMembershipOn(date) is not null;

    public Payment PurchaseMembership(MembershipPlan plan, DateOnly startsOn, PaymentMethod method, DateTimeOffset now)
    {
        if (!plan.IsActive)
            throw new DomainException($"Membership plan '{plan.Name}' is not available for sale.");

        if (startsOn < now.ToDateOnly())
            throw new DomainException("A membership cannot start in the past.");

        var membership = Membership.Create(plan, startsOn, now);
        if (_memberships.Any(m => m.Blocks(membership.StartsOn, membership.EndsOn)))
            throw new DomainException("The client already has a membership for these dates.");

        var payment = Payment.ForMembership(Id, membership, method, now);
        _memberships.Add(membership);
        return payment;
    }

    public void CancelMembership(Guid membershipId, DateTimeOffset now) => FindMembership(membershipId).Cancel(now);

    public Visit CheckIn(DateTimeOffset now)
    {
        var today = now.ToDateOnly();
        var membership = ActiveMembershipOn(today)
            ?? throw new DomainException("The client has no active membership today.");

        membership.RegisterVisit(today);
        return Visit.Record(Id, membership.Id, now);
    }

    public bool Owns(Membership membership) => _memberships.Contains(membership);

    public bool NeedsExpiryNotice(Membership membership, DateOnly today) =>
        Owns(membership)
        && !membership.IsCancelled
        && membership.HasVisitsRemaining
        && membership.EndsOn >= today
        && !_memberships.Any(other => other != membership && !other.IsCancelled && other.StartsOn > membership.StartsOn);

    public IReadOnlyList<Membership> MembershipsNeedingExpiryNotice(DateOnly today, DateOnly endsBy) =>
        _memberships.Where(m => m.EndsOn <= endsBy && NeedsExpiryNotice(m, today)).ToList();

    private Membership FindMembership(Guid membershipId) =>
        _memberships.FirstOrDefault(m => m.Id == membershipId)
        ?? throw new DomainException($"Membership '{membershipId}' does not belong to this client.");

    private static int AgeOn(DateOnly dateOfBirth, DateOnly date)
    {
        var age = date.Year - dateOfBirth.Year;
        return date < dateOfBirth.AddYears(age) ? age - 1 : age;
    }
}
