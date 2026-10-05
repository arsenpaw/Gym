using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record Money
{
    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static Money Of(decimal amount)
    {
        if (amount < 0)
            throw new DomainException("Amount cannot be negative.");

        if (decimal.Round(amount, 2) != amount)
            throw new DomainException("Amount can have at most 2 decimal places.");

        return new Money(amount);
    }
}
