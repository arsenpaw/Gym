using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Payments;

public sealed class Payment : AggregateRoot
{
    public Guid ClientId { get; private set; }
    public Guid MembershipId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public PaymentMethod Method { get; private set; }
    public DateTimeOffset PaidAt { get; private set; }

    private Payment()
    {
    }

    internal static Payment ForMembership(Guid clientId, Clients.Membership membership, PaymentMethod method, DateTimeOffset paidAt)
    {
        if (!Enum.IsDefined(method))
            throw new DomainException("Unknown payment method.");

        return new Payment
        {
            ClientId = clientId,
            MembershipId = membership.Id,
            Amount = membership.Price,
            Method = method,
            PaidAt = paidAt,
        };
    }
}
