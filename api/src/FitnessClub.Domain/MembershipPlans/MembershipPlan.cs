using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.MembershipPlans;

public sealed class MembershipPlan : Entity
{
    public const int NameMaxLength = 100;
    public const int MaxValidityDays = 3650;
    public const int MaxVisitLimit = 1000;

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
    public int ValidityDays { get; private set; }
    public int? VisitLimit { get; private set; }
    public bool IsActive { get; private set; }

    private MembershipPlan()
    {
    }

    public static MembershipPlan Create(string name, decimal price, int validityDays, int? visitLimit)
    {
        var plan = new MembershipPlan { IsActive = true };
        plan.Update(name, price, validityDays, visitLimit);
        return plan;
    }

    public void Update(string name, decimal price, int validityDays, int? visitLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Plan name is required.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
            throw new DomainException($"Plan name must be at most {NameMaxLength} characters.");

        if (price <= 0)
            throw new DomainException("Price must be greater than zero.");

        if (decimal.Round(price, 2) != price)
            throw new DomainException("Price can have at most 2 decimal places.");

        if (validityDays is < 1 or > MaxValidityDays)
            throw new DomainException($"Validity must be between 1 and {MaxValidityDays} days.");

        if (visitLimit is < 1 or > MaxVisitLimit)
            throw new DomainException($"Visit limit must be between 1 and {MaxVisitLimit}, or empty for unlimited.");

        Name = trimmedName;
        Price = price;
        ValidityDays = validityDays;
        VisitLimit = visitLimit;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
