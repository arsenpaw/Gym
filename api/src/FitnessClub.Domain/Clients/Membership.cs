using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Clients;

public sealed class Membership : Entity
{
    public Guid PlanId { get; private set; }
    public string PlanName { get; private set; } = null!;
    public Money Price { get; private set; } = null!;
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public int? VisitLimit { get; private set; }
    public int VisitsUsed { get; private set; }
    public DateOnly? LastVisitOn { get; private set; }
    public DateTimeOffset PurchasedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    private Membership()
    {
    }

    internal static Membership Create(MembershipPlan plan, DateOnly startsOn, DateTimeOffset purchasedAt) =>
        new()
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            Price = plan.Price,
            StartsOn = startsOn,
            EndsOn = startsOn.AddDays(plan.ValidityDays - 1),
            VisitLimit = plan.VisitLimit,
            PurchasedAt = purchasedAt,
        };

    public bool IsCancelled => CancelledAt is not null;

    public int? RemainingVisits => VisitLimit - VisitsUsed;

    public bool HasVisitsRemaining => VisitLimit is null || VisitsUsed < VisitLimit;

    public bool IsActiveOn(DateOnly date) => !IsCancelled && HasVisitsRemaining && StartsOn <= date && date <= EndsOn;

    internal bool Blocks(DateOnly startsOn, DateOnly endsOn) =>
        !IsCancelled && HasVisitsRemaining && StartsOn <= endsOn && startsOn <= EndsOn;

    internal void RegisterVisit(DateOnly today)
    {
        if (!HasVisitsRemaining)
            throw new DomainException("The membership has no visits left.");

        if (LastVisitOn == today)
            throw new DomainException("The client has already checked in today.");

        VisitsUsed++;
        LastVisitOn = today;
    }

    internal void Cancel(DateTimeOffset now)
    {
        if (IsCancelled)
            throw new DomainException("The membership is already cancelled.");

        if (EndsOn < now.ToDateOnly())
            throw new DomainException("An expired membership cannot be cancelled.");

        CancelledAt = now;
    }
}
