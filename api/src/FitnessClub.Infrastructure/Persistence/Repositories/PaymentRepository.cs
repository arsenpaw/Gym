using FitnessClub.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRepository(FitnessClubDbContext db) : Repository<Payment>(db), IPaymentRepository
{
    public async Task<IReadOnlyList<Payment>> ListPaidBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await Set.Where(p => p.PaidAt >= from && p.PaidAt < to).OrderBy(p => p.PaidAt).ToListAsync(cancellationToken);
}
