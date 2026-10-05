using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Payments;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IReadOnlyList<Payment>> ListPaidBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
