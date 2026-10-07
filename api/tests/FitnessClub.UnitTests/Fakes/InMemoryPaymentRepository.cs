using FitnessClub.Domain.Payments;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly List<Payment> _payments = [];

    public IReadOnlyList<Payment> All => _payments;

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_payments.FirstOrDefault(p => p.Id == id));

    public void Add(Payment aggregate) => _payments.Add(aggregate);

    public Task<IReadOnlyList<Payment>> ListPaidBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Payment>>(
            _payments.Where(p => p.PaidAt >= from && p.PaidAt < to).OrderBy(p => p.PaidAt).ToList());
}
