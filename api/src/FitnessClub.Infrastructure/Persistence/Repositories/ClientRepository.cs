using FitnessClub.Domain.Clients;
using FitnessClub.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class ClientRepository(FitnessClubDbContext db) : Repository<Client>(db), IClientRepository
{
    public async Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken) =>
        await Set.OrderBy(c => c.Name.LastName).ThenBy(c => c.Name.FirstName).ToListAsync(cancellationToken);

    public Task<bool> EmailExistsAsync(EmailAddress email, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(c => c.Id != excludeId && c.Email == email, cancellationToken);

    public async Task<IReadOnlyList<Client>> ListWithMembershipsEndingBetweenAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await Set
            .Where(c => c.Memberships.Any(m => m.CancelledAt == null && m.EndsOn >= from && m.EndsOn <= to))
            .ToListAsync(cancellationToken);
}
